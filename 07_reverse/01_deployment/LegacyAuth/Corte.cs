using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace LegacyAuth;

/// <summary>
/// Corte de la migración (fin de la ventana, p. ej. mes 6).
///
/// Requisito: el proveedor de migración ya está DESACTIVADO en Keycloak
/// (User federation -> legacy-migration -> Enabled = off). Si sigue activo,
/// crear usuarios dispararía otra vez la consulta al legacy.
///
/// Para cada usuario del legacy:
///   - Ya existe en Keycloak con contraseña  -> migrado, no se toca.
///   - Existe sin contraseña (lo consultó el plugin pero nunca completó el login) -> se deshabilita.
///   - No existe -> se crea deshabilitado con su rol.
/// Antes de aplicar, desliga del proveedor a los usuarios que aún tengan la liga.
///
/// Uso:  corte              (simulación, no cambia nada)
///       corte --aplicar
/// </summary>
public static class Corte
{
    public static async Task<int> EjecutarAsync(string[] args)
    {
        var aplicar = args.Contains("--aplicar");
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        string Requerida(string clave) =>
            config[clave] is { Length: > 0 } valor
                ? valor
                : throw new InvalidOperationException($"Falta la variable {clave.Replace(":", "__")}");

        var realm = Requerida("Corte:Realm");
        var proveedor = Requerida("Corte:ProviderName");
        var repositorio = new LegacyUserRepository(Requerida("ConnectionStrings:Legacy"));

        using var http = new HttpClient { BaseAddress = new Uri(Requerida("Corte:KeycloakUrl").TrimEnd('/') + "/") };
        var token = await ObtenerTokenAsync(http, realm, Requerida("Corte:ClientId"), Requerida("Corte:ClientSecret"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var admin = $"admin/realms/{realm}/";

        var componentes = await http.GetFromJsonAsync<JsonArray>(
            $"{admin}components?type=org.keycloak.storage.UserStorageProvider&name={Uri.EscapeDataString(proveedor)}");
        if (componentes is not [JsonObject componente])
        {
            Console.Error.WriteLine($"No se encontró el proveedor '{proveedor}' en el realm '{realm}'.");
            return 2;
        }

        if (componente["config"]?["enabled"]?[0]?.GetValue<string>() != "false")
        {
            Console.Error.WriteLine($"El proveedor '{proveedor}' sigue ACTIVO. Desactívalo en User federation y vuelve a ejecutar.");
            return 2;
        }

        // 1. Plan
        var plan = new List<(UsuarioLegacy Usuario, Accion Accion, string? KeycloakId)>();
        foreach (var usuario in await repositorio.ListarAsync(CancellationToken.None))
        {
            var username = usuario.Username.ToLowerInvariant();
            var encontrados = await http.GetFromJsonAsync<JsonArray>(
                $"{admin}users?username={Uri.EscapeDataString(username)}&exact=true");

            if (encontrados is not [JsonObject existente, ..])
            {
                plan.Add((usuario, Accion.CrearDeshabilitado, null));
                continue;
            }

            var id = existente["id"]!.GetValue<string>();
            var credenciales = await http.GetFromJsonAsync<JsonArray>($"{admin}users/{id}/credentials");
            var tienePassword = credenciales!.Any(c => c?["type"]?.GetValue<string>() == "password");
            var habilitado = existente["enabled"]!.GetValue<bool>();
            // Con el proveedor apagado, Keycloak MUESTRA deshabilitados a los usuarios aún ligados,
            // pero al desligarlos recuperan su "enabled" guardado: hay que deshabilitarlos explícitamente.
            var ligado = existente["federationLink"] is not null;

            plan.Add((usuario, tienePassword ? Accion.YaMigrado : habilitado || ligado ? Accion.Deshabilitar : Accion.YaDeshabilitado, id));
        }

        Console.WriteLine($"{"USUARIO",-25} {"ROL",-10} ACCIÓN");
        foreach (var (usuario, accion, _) in plan)
        {
            Console.WriteLine($"{usuario.Username,-25} {usuario.Rol,-10} {accion}");
        }

        Console.WriteLine();
        foreach (var grupo in plan.GroupBy(p => p.Accion))
        {
            Console.WriteLine($"{grupo.Key}: {grupo.Count()}");
        }

        if (!aplicar)
        {
            Console.WriteLine("\nSimulación: no se cambió nada. Ejecuta con --aplicar para aplicar el plan.");
            return 0;
        }

        // 2. Aplicar
        var desligar = await http.PostAsync($"{admin}user-storage/{componente["id"]!.GetValue<string>()}/unlink-users", null);
        desligar.EnsureSuccessStatusCode();

        foreach (var (usuario, accion, keycloakId) in plan)
        {
            switch (accion)
            {
                case Accion.CrearDeshabilitado:
                    var creado = await http.PostAsJsonAsync($"{admin}users", new { username = usuario.Username.ToLowerInvariant(), enabled = false });
                    creado.EnsureSuccessStatusCode();
                    var nuevoId = creado.Headers.Location!.Segments[^1];

                    var rol = await http.GetFromJsonAsync<JsonObject>($"{admin}roles/{Uri.EscapeDataString(usuario.Rol)}");
                    (await http.PostAsJsonAsync($"{admin}users/{nuevoId}/role-mappings/realm", new[] { rol })).EnsureSuccessStatusCode();
                    break;

                case Accion.Deshabilitar:
                    var representacion = await http.GetFromJsonAsync<JsonObject>($"{admin}users/{keycloakId}");
                    representacion!["enabled"] = false;
                    (await http.PutAsJsonAsync($"{admin}users/{keycloakId}", representacion)).EnsureSuccessStatusCode();
                    break;
            }
        }

        Console.WriteLine("\nCorte aplicado.");
        return 0;
    }

    private static async Task<string> ObtenerTokenAsync(HttpClient http, string realm, string clientId, string clientSecret)
    {
        var respuesta = await http.PostAsync($"realms/{realm}/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        }));
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonObject>();
        return cuerpo!["access_token"]!.GetValue<string>();
    }

    private enum Accion
    {
        YaMigrado,
        YaDeshabilitado,
        Deshabilitar,
        CrearDeshabilitado,
    }
}

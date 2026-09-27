using System.Security.Cryptography;
using System.Text;
using LegacyAuth;

// Servicio TEMPORAL de migración de usuarios legacy -> Keycloak.
// Implementa el contrato del plugin keycloak-user-migration:
//   GET  /user-migration-support/{usuario}  -> 200 con datos del usuario, 404 si no existe
//   POST /user-migration-support/{usuario}  -> 200 si la contraseña es válida, 401 si no
// Se elimina cuando termina la ventana de migración (ver README, "Corte").

if (args.FirstOrDefault() == "corte")
{
    return await Corte.EjecutarAsync(args.Skip(1).ToArray());
}

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Legacy");
var apiToken = builder.Configuration["LegacyAuth:ApiToken"];
if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(apiToken))
{
    throw new InvalidOperationException("Faltan ConnectionStrings__Legacy o LegacyAuth__ApiToken.");
}

builder.Services.AddSingleton(new LegacyUserRepository(connectionString));

var app = builder.Build();

var authorizationEsperado = Encoding.UTF8.GetBytes($"Bearer {apiToken}");

app.MapGet("/healthz", () => Results.Ok());

// Solo Keycloak conoce el token. NUNCA registrar el cuerpo de las peticiones: trae contraseñas.
var migracion = app.MapGroup("/user-migration-support")
    .AddEndpointFilter(async (context, next) =>
    {
        var recibido = Encoding.UTF8.GetBytes(context.HttpContext.Request.Headers.Authorization.ToString());
        return CryptographicOperations.FixedTimeEquals(recibido, authorizationEsperado)
            ? await next(context)
            : Results.Unauthorized();
    });

// El plugin también consulta por email (p. ej. "olvidé mi contraseña"); el legacy no tiene email,
// así que esas búsquedas terminan en 404.
migracion.MapGet("/{usuarioOEmail}", async (string usuarioOEmail, LegacyUserRepository usuarios, ILogger<Program> logger, CancellationToken ct) =>
{
    var usuario = await usuarios.BuscarAsync(usuarioOEmail, ct);
    if (usuario is null)
    {
        return Results.NotFound();
    }

    logger.LogInformation("Usuario legacy {Username} consultado por Keycloak", usuario.Username);

    // Sin email ni nombre: Keycloak los pide en el primer login (perfil obligatorio + verificación de email)
    return Results.Ok(new
    {
        username = usuario.Username,
        enabled = usuario.Activo,
        emailVerified = false,
        roles = new[] { usuario.Rol },
    });
});

migracion.MapPost("/{usuario}", async (string usuario, SolicitudPassword solicitud, LegacyUserRepository usuarios, ILogger<Program> logger, CancellationToken ct) =>
{
    var valida = await usuarios.ValidarPasswordAsync(usuario, solicitud.Password, ct);
    logger.LogInformation("Validación de contraseña para {Username}: {Resultado}", usuario, valida ? "válida" : "inválida");

    return valida ? Results.Ok() : Results.Unauthorized();
});

app.Run();
return 0;

internal sealed record SolicitudPassword(string Password);

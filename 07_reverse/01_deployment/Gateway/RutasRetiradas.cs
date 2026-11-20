using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

/// <summary>
/// Retiro de las apps viejas (sección "RutasRetiradas" de appsettings.json).
/// Todo lo que empiece con un prefijo retirado (/legacy = WebForms, /taxes = MVC "Second System Broken")
/// se redirige a /rutas-retiradas, una página del propio gateway con un botón de regreso al inicio.
/// Las excepciones son páginas ya migradas a Angular que conservan su URL bajo /legacy.
/// Rollback: "RutasRetiradas:Habilitado": false (o RutasRetiradas__Habilitado=false en el contenedor)
/// y /legacy y /taxes vuelven a pasar al proxy hacia IIS como antes.
/// </summary>
public sealed class RutasRetiradasOptions
{
    public const string Seccion = "RutasRetiradas";

    public bool Habilitado { get; set; }

    /// <summary>Prefijos completos por segmento: "/legacy" cubre /legacy y /legacy/..., no /legacyx.</summary>
    public string[] Prefijos { get; set; } = [];

    /// <summary>Se comparan con StartsWith: "/legacy/AngularPage" cubre también AngularPage.aspx.</summary>
    public string[] Excepciones { get; set; } = [];
}

public static class RutasRetiradas
{
    public const string RutaPagina = "/rutas-retiradas";

    public static IServiceCollection AddRutasRetiradas(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RutasRetiradasOptions>(configuration.GetSection(RutasRetiradasOptions.Seccion));
        return services;
    }

    /// <summary>Va antes de los endpoints y del proxy: intercepta la petición antes de que salga a IIS.</summary>
    public static IApplicationBuilder UseRutasRetiradas(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var opciones = context.RequestServices.GetRequiredService<IOptionsMonitor<RutasRetiradasOptions>>().CurrentValue;
            var ruta = context.Request.Path;

            if (opciones.Habilitado && EstaRetirada(ruta, opciones))
            {
                var desde = Uri.EscapeDataString(ruta.Value ?? string.Empty);
                context.Response.Redirect($"{RutaPagina}?desde={desde}");
                return;
            }

            await next(context);
        });

    public static IEndpointRouteBuilder MapRutasRetiradas(this IEndpointRouteBuilder app)
    {
        app.MapMethods(RutaPagina, [HttpMethods.Get, HttpMethods.Head], (string? desde) =>
            Results.Content(Pagina(desde), "text/html; charset=utf-8", statusCode: StatusCodes.Status410Gone));
        return app;
    }

    private static bool EstaRetirada(PathString ruta, RutasRetiradasOptions opciones)
    {
        if (!ruta.HasValue)
        {
            return false;
        }

        foreach (var excepcion in opciones.Excepciones)
        {
            if (ruta.Value.StartsWith(excepcion, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var prefijo in opciones.Prefijos)
        {
            // StartsWithSegments: /legacy, /legacy/ y /legacy/x sí; /legacyx no
            if (ruta.StartsWithSegments(prefijo, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Pagina(string? desde)
    {
        var detalle = string.IsNullOrWhiteSpace(desde)
            ? "La página que buscabas pertenecía a un sistema anterior que ya fue retirado."
            : $"La dirección <code>{HtmlEncoder.Default.Encode(desde.Length > 200 ? desde[..200] : desde)}</code> pertenecía a un sistema anterior que ya fue retirado.";

        return $$"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="robots" content="noindex">
              <title>Rutas retiradas</title>
              <style>
                :root { --fondo: #f5f6f8; --tarjeta: #ffffff; --texto: #1f2933; --suave: #52606d; --borde: #d9dee5; --acento: #1d4ed8; --acento-texto: #ffffff; }
                @media (prefers-color-scheme: dark) {
                  :root { --fondo: #111418; --tarjeta: #1b2027; --texto: #e6e9ee; --suave: #a3adba; --borde: #2c333d; --acento: #60a5fa; --acento-texto: #0b1220; }
                }
                * { box-sizing: border-box; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 16px;
                       background: var(--fondo); color: var(--texto);
                       font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
                main { max-width: 480px; width: 100%; background: var(--tarjeta); border: 1px solid var(--borde);
                       border-radius: 12px; padding: 32px 24px; text-align: center; }
                .codigo { margin: 0; font-size: 14px; font-weight: 600; letter-spacing: .08em; color: var(--suave); }
                h1 { margin: 8px 0 12px; font-size: 24px; line-height: 1.3; }
                p { margin: 0 0 12px; color: var(--suave); line-height: 1.5; }
                code { font-size: 13px; word-break: break-all; color: var(--texto); }
                .boton { display: inline-block; margin-top: 12px; padding: 10px 20px; border-radius: 8px;
                         background: var(--acento); color: var(--acento-texto); text-decoration: none; font-weight: 600; }
                .boton:hover { filter: brightness(1.08); }
                .boton:focus-visible { outline: 3px solid var(--acento); outline-offset: 3px; }
              </style>
            </head>
            <body>
              <main>
                <p class="codigo">410</p>
                <h1>Estas rutas ya no existen</h1>
                <p>{{detalle}}</p>
                <p>Sentimos las molestias.</p>
                <a class="boton" href="/">Volver al inicio</a>
              </main>
            </body>
            </html>
            """;
    }
}

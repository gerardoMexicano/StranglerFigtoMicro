using System;
using System.Collections.Generic;
using Microsoft.Owin;
using Microsoft.Owin.Host.SystemWeb;
using Microsoft.Owin.Infrastructure;
using Owin;

namespace WebApp_MVC
{
    public static class OwinProxy
    {
        // Detrás de nginx + gateway, IIS ve http://192.168.50.243:8081/taxes/... pero el navegador ve el
        // dominio público. Katana arma URLs absolutas (regreso después del login) y decide si la cookie
        // es Secure con lo que ve IIS; esto le devuelve el esquema y el host originales.
        // Solo se confía en las cabeceras si la petición viene de un proxy conocido: IIS también es
        // alcanzable directo en la LAN y cualquiera podría mandarlas.
        public static void UsarEncabezadosDelProxy(this IAppBuilder app, string proxiesConfiables)
        {
            var confiables = new HashSet<string>(
                (proxiesConfiables ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries));

            // Sin proxy (Visual Studio / IIS Express): no hay nada que corregir
            if (confiables.Count == 0)
            {
                return;
            }

            app.Use((contexto, siguiente) =>
            {
                if (confiables.Contains(contexto.Request.RemoteIpAddress))
                {
                    string esquema = PrimerValor(contexto.Request.Headers["X-Forwarded-Proto"]);
                    string host = PrimerValor(contexto.Request.Headers["X-Forwarded-Host"]);

                    if (esquema != null)
                    {
                        contexto.Request.Scheme = esquema;
                    }

                    if (host != null)
                    {
                        contexto.Request.Host = new HostString(host);
                    }
                }

                return siguiente();
            });
        }

        // Si pasó por varios proxies, el primero de la lista es el que vio el navegador
        private static string PrimerValor(string valor)
        {
            return string.IsNullOrWhiteSpace(valor) ? null : valor.Split(',')[0].Trim();
        }
    }

    // Katana crea la cookie OpenIdConnect.nonce con SameSite=None, y el navegador la descarta si no
    // va por https: el nonce se pierde y el login falla al volver de Keycloak. Con ResponseMode=Query
    // Keycloak regresa con un GET normal entre sitios, así que SameSite=Lax basta.
    public class SameSiteLaxCookieManager : ICookieManager
    {
        private readonly ICookieManager interno = new SystemWebCookieManager();

        public string GetRequestCookie(IOwinContext context, string key)
        {
            return interno.GetRequestCookie(context, key);
        }

        public void AppendResponseCookie(IOwinContext context, string key, string value, CookieOptions options)
        {
            interno.AppendResponseCookie(context, key, value, ALax(options));
        }

        public void DeleteCookie(IOwinContext context, string key, CookieOptions options)
        {
            interno.DeleteCookie(context, key, ALax(options));
        }

        private static CookieOptions ALax(CookieOptions options)
        {
            if (options.SameSite == SameSiteMode.None)
            {
                options.SameSite = SameSiteMode.Lax;
            }

            return options;
        }
    }
}

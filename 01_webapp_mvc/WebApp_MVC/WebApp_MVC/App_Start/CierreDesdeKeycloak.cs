using System;
using System.Security.Claims;

namespace WebApp_MVC
{
    // Front-channel logout (logout global). Cuando el usuario cierra sesión en cualquier app (MVC,
    // WebForms o /newapp), Keycloak cierra la sesión SSO y carga en un iframe, por cada app con sesión,
    // su frontchannel.logout.url con ?iss=...&sid=... Aquí se decide si esa petición es de la sesión
    // de este navegador: si lo es, se borra la cookie local (y con ella la sesión de SesionEnServidor).
    // Keycloak ya cerró la suya, así que no se vuelve a llamar al end_session_endpoint.
    public static class CierreDesdeKeycloak
    {
        public static bool EsDeEstaSesion(ClaimsPrincipal usuario, string iss, string sid, string authority)
        {
            if (usuario == null || usuario.Identity == null || !usuario.Identity.IsAuthenticated)
            {
                return false;
            }

            // Solo el Keycloak de esta app (oidc:Authority) puede pedir el cierre
            if (!string.Equals((iss ?? "").TrimEnd('/'), (authority ?? "").TrimEnd('/'), StringComparison.Ordinal))
            {
                return false;
            }

            // Katana guarda el "sid" del id_token como ClaimTypes.Sid (mapeo por defecto del handler JWT).
            // Debe coincidir: así otra página no puede cerrar la sesión con un iframe sin conocer el sid.
            Claim sidDeLaSesion = usuario.FindFirst(ClaimTypes.Sid) ?? usuario.FindFirst("sid");
            if (sidDeLaSesion == null)
            {
                // Sesión sin sid (no debería pasar con Keycloak): sin con qué comparar, se cierra
                return true;
            }

            return string.Equals(sidDeLaSesion.Value, sid, StringComparison.Ordinal);
        }
    }
}

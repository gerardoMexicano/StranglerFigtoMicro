using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;

namespace WebApp_MVC
{
    // La sesión (claims + access, refresh e id token de Keycloak por SaveTokens) se queda en la memoria
    // de IIS y la cookie .AUTH.MVC solo lleva su llave cifrada (unos cientos de bytes). Con todo adentro
    // la cookie ocupaba varios KB: el navegador la descartaba (de ahí los trozos), nginx daba 502 al
    // responder el login y, con las cookies de MVC y WebForms juntas, el encabezado Cookie puede rebasar
    // el límite de nginx (8 KB por línea: 400 "Request Header Or Cookie Too Large").
    // Si IIS recicla el app pool se pierden las sesiones: OIDC vuelve a Keycloak y, con su sesión
    // abierta, el usuario entra sin escribir la contraseña.
    public class SesionEnServidor : IAuthenticationSessionStore
    {
        private const string Prefijo = "sesion-owin:";

        // Respaldo por si el ticket no trae vencimiento (el middleware de cookies siempre lo pone)
        private readonly TimeSpan _duracion;

        public SesionEnServidor(TimeSpan duracion)
        {
            _duracion = duracion;
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            string llave = Guid.NewGuid().ToString("N");
            await RenewAsync(llave, ticket);
            return llave;
        }

        // Vence junto con la cookie; NotRemovable para que IIS no la tire por presión de memoria
        public Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            DateTimeOffset vence = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.Add(_duracion);
            HttpRuntime.Cache.Insert(Prefijo + key, ticket, null, vence.UtcDateTime, Cache.NoSlidingExpiration,
                CacheItemPriority.NotRemovable, null);
            return Task.FromResult(0);
        }

        // null = la sesión ya no existe (venció o se recicló el app pool): el usuario queda anónimo
        public Task<AuthenticationTicket> RetrieveAsync(string key)
        {
            return Task.FromResult(HttpRuntime.Cache.Get(Prefijo + key) as AuthenticationTicket);
        }

        public Task RemoveAsync(string key)
        {
            HttpRuntime.Cache.Remove(Prefijo + key);
            return Task.FromResult(0);
        }
    }
}

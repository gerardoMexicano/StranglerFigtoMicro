using System;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Microsoft.Owin.Host.SystemWeb;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Notifications;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;

// Microsoft.Owin.Host.SystemWeb busca esta clase al arrancar la app en IIS.
// Sin ella, la app no inicia ("No assembly found containing an OwinStartupAttribute").
[assembly: OwinStartup(typeof(WebApp_MVC.Startup))]

namespace WebApp_MVC
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Primero: que el resto del pipeline vea el esquema y host públicos (ver OwinProxy.cs)
            app.UsarEncabezadosDelProxy(ConfigurationManager.AppSettings["proxy:Confiables"]);

            // Cuando OIDC termina de validar al usuario, se lo entrega a este middleware de cookies
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            // "¿Quién eres en ESTA app?": la sesión local, como antes .AUTH
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = CookieAuthenticationDefaults.AuthenticationType,
                // Nombre propio: MVC y WebForms comparten host (localhost, y el dominio del gateway);
                // cada app tiene su sesión y el SSO entre ellas lo da la sesión de Keycloak
                CookieName = ".AUTH.MVC",
                ExpireTimeSpan = TimeSpan.FromMinutes(int.Parse(ConfigurationManager.AppSettings["auth:SesionMinutos"])),
                SlidingExpiration = true,
                CookieHttpOnly = true,
                CookieSecure = CookieSecureOption.SameAsRequest,
                // Evita que System.Web y OWIN se pisen las cookies de la respuesta (bug conocido de Katana)
                // y parte la cookie en trozos de ~4 KB: con claims + id_token pasa del límite del navegador
                CookieManager = new SystemWebChunkingCookieManager()
            });

            // "No hay cookie: ¿cómo averiguo quién eres?": Authorization Code + PKCE contra Keycloak
            app.UseOpenIdConnectAuthentication(new OpenIdConnectAuthenticationOptions
            {
                Authority = ConfigurationManager.AppSettings["oidc:Authority"],
                ClientId = ConfigurationManager.AppSettings["oidc:ClientId"],
                ClientSecret = ConfigurationManager.AppSettings["oidc:ClientSecret"],
                // Su ruta (/signin-oidc) es donde este middleware escucha la respuesta de Keycloak
                RedirectUri = ConfigurationManager.AppSettings["oidc:RedirectUri"],
                PostLogoutRedirectUri = ConfigurationManager.AppSettings["oidc:PostLogoutRedirectUri"],
                RequireHttpsMetadata = bool.Parse(ConfigurationManager.AppSettings["oidc:RequireHttpsMetadata"]),

                ResponseType = OpenIdConnectResponseType.Code,
                // Keycloak regresa con un GET (?code=...): no depende de cookies SameSite=None como form_post
                ResponseMode = OpenIdConnectResponseMode.Query,
                RedeemCode = true,
                UsePkce = true,
                Scope = "openid profile email",
                // Guarda el id_token en la cookie: el logout (paso 4) lo manda a Keycloak como id_token_hint
                SaveTokens = true,
                // Sin esto la cookie duraría lo que el id_token (5 min en el realm) en vez de auth:SesionMinutos
                UseTokenLifetime = false,

                TokenValidationParameters = new TokenValidationParameters
                {
                    // User.Identity.Name = username del legacy (el plugin de migración lo conserva)
                    NameClaimType = "preferred_username"
                },

                Notifications = new OpenIdConnectAuthenticationNotifications
                {
                    RedirectToIdentityProvider = AgregarIdTokenHintAlLogout
                },

                // Cookie del nonce con SameSite=Lax para que sobreviva sin https (ver OwinProxy.cs)
                CookieManager = new SameSiteLaxCookieManager()
            });
        }

        // Logout (RP-initiated): Keycloak solo vuelve a post_logout_redirect_uri sin preguntar
        // "¿seguro que quieres salir?" si sabe de qué sesión se trata. El id_token guardado
        // en la cookie (SaveTokens) se lo dice; client_id es el respaldo si no lo hay.
        private static async Task AgregarIdTokenHintAlLogout(
            RedirectToIdentityProviderNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> n)
        {
            if (n.ProtocolMessage.RequestType != OpenIdConnectRequestType.Logout)
            {
                return;
            }

            n.ProtocolMessage.ClientId = n.Options.ClientId;

            AuthenticateResult sesion = await n.OwinContext.Authentication
                .AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationType);

            string idToken;
            if (sesion != null && sesion.Properties.Dictionary.TryGetValue(OpenIdConnectParameterNames.IdToken, out idToken))
            {
                n.ProtocolMessage.IdTokenHint = idToken;
            }
        }
    }
}

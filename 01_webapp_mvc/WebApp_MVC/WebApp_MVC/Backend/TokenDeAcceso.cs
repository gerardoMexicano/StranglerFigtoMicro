using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Newtonsoft.Json.Linq;

namespace WebApp_MVC.Backend
{
    // Access token del usuario para llamar a 05_backend.
    // Startup guarda los tokens de Keycloak en la sesión (SaveTokens), que vive en IIS (SesionEnServidor).
    // El access token dura minutos y la sesión una hora: cuando vence se renueva con el refresh token y se
    // guarda una sesión nueva.
    // null = ya no hay forma de renovarlo (sesión de Keycloak cerrada o vencida) y hay que volver a
    // hacer login.
    public static class TokenDeAcceso
    {
        // Renovar un poco antes de que venza, para que no expire en camino al backend
        private static readonly TimeSpan Margen = TimeSpan.FromSeconds(30);

        private const string ClaveVence = "expires_at";

        private static readonly HttpClient Http = new HttpClient();

        // Lee el token_endpoint del discovery de Keycloak y lo cachea
        private static readonly ConfigurationManager<OpenIdConnectConfiguration> Metadatos =
            new ConfigurationManager<OpenIdConnectConfiguration>(
                ConfigurationManager.AppSettings["oidc:Authority"].TrimEnd('/') + "/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever
                {
                    RequireHttps = bool.Parse(ConfigurationManager.AppSettings["oidc:RequireHttpsMetadata"])
                });

        public static async Task<string> ObtenerAsync(IOwinContext contexto)
        {
            AuthenticateResult sesion = await contexto.Authentication
                .AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationType);
            if (sesion == null || sesion.Identity == null)
            {
                return null;
            }

            IDictionary<string, string> tokens = sesion.Properties.Dictionary;

            string accessToken;
            if (tokens.TryGetValue(OpenIdConnectParameterNames.AccessToken, out accessToken) && !Vencido(tokens))
            {
                return accessToken;
            }

            string refreshToken;
            if (!tokens.TryGetValue(OpenIdConnectParameterNames.RefreshToken, out refreshToken))
            {
                return null;
            }

            JObject nuevos = await RenovarAsync(refreshToken);
            if (nuevos == null)
            {
                return null;
            }

            Guardar(tokens, nuevos);

            // Misma identidad, tokens nuevos: el middleware de cookies guarda el ticket en SesionEnServidor y
            // reemite .AUTH.MVC con la llave nueva al responder
            sesion.Properties.IssuedUtc = null;
            sesion.Properties.ExpiresUtc = null;
            contexto.Authentication.SignIn(sesion.Properties, sesion.Identity);

            return tokens[OpenIdConnectParameterNames.AccessToken];
        }

        // Sin expires_at no se sabe: se usa el token y, si ya venció, el backend responde 401
        private static bool Vencido(IDictionary<string, string> tokens)
        {
            string valor;
            DateTimeOffset vence;
            if (!tokens.TryGetValue(ClaveVence, out valor) ||
                !DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out vence))
            {
                return false;
            }

            return vence - Margen <= DateTimeOffset.UtcNow;
        }

        // grant_type=refresh_token contra Keycloak. null si lo rechaza (refresh vencido o revocado).
        private static async Task<JObject> RenovarAsync(string refreshToken)
        {
            OpenIdConnectConfiguration configuracion = await Metadatos.GetConfigurationAsync(CancellationToken.None);

            var cuerpo = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
                { "client_id", ConfigurationManager.AppSettings["oidc:ClientId"] },
                { "client_secret", ConfigurationManager.AppSettings["oidc:ClientSecret"] }
            });

            using (HttpResponseMessage respuesta = await Http.PostAsync(configuracion.TokenEndpoint, cuerpo))
            {
                if (!respuesta.IsSuccessStatusCode)
                {
                    return null;
                }

                return JObject.Parse(await respuesta.Content.ReadAsStringAsync());
            }
        }

        // Mismas claves que usa SaveTokens de Katana, para que el logout siga encontrando el id_token
        private static void Guardar(IDictionary<string, string> tokens, JObject nuevos)
        {
            string[] nombres =
            {
                OpenIdConnectParameterNames.AccessToken,
                OpenIdConnectParameterNames.RefreshToken,
                OpenIdConnectParameterNames.IdToken
            };

            foreach (string nombre in nombres)
            {
                string valor = nuevos.Value<string>(nombre);
                if (!string.IsNullOrEmpty(valor))
                {
                    tokens[nombre] = valor;
                }
            }

            int? segundos = nuevos.Value<int?>(OpenIdConnectParameterNames.ExpiresIn);
            if (segundos.HasValue)
            {
                tokens[ClaveVence] = DateTimeOffset.UtcNow.AddSeconds(segundos.Value).ToString("o", CultureInfo.InvariantCulture);
            }
            else
            {
                tokens.Remove(ClaveVence);
            }
        }
    }
}

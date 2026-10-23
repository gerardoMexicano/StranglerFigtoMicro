Imports System.Threading.Tasks
Imports Microsoft.IdentityModel.Protocols.OpenIdConnect
Imports Microsoft.IdentityModel.Tokens
Imports Microsoft.Owin
Imports Microsoft.Owin.Host.SystemWeb
Imports Microsoft.Owin.Security
Imports Microsoft.Owin.Security.Cookies
Imports Microsoft.Owin.Security.Notifications
Imports Microsoft.Owin.Security.OpenIdConnect
Imports Owin

' Microsoft.Owin.Host.SystemWeb busca esta clase al arrancar la app en IIS.
' Sin ella, la app no inicia ("No assembly found containing an OwinStartupAttribute").
<Assembly: OwinStartup(GetType(Startup))>

' Mismo pipeline que WebApp_MVC/Startup.cs, con el cliente legacy-webforms.
Public Class Startup

    Public Sub Configuration(app As IAppBuilder)
        ' Primero: que el resto del pipeline vea el esquema y host públicos (ver App_Start/OwinProxy.vb)
        app.UsarEncabezadosDelProxy(ConfigurationManager.AppSettings("proxy:Confiables"))

        ' Cuando OIDC termina de validar al usuario, se lo entrega a este middleware de cookies
        app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType)

        ' "¿Quién eres en ESTA app?": la sesión local, como antes .AUTH
        app.UseCookieAuthentication(New CookieAuthenticationOptions With {
            .AuthenticationType = CookieAuthenticationDefaults.AuthenticationType,
            .CookieName = ".AUTH.WEBFORMS",
            .ExpireTimeSpan = TimeSpan.FromMinutes(Integer.Parse(ConfigurationManager.AppSettings("auth:SesionMinutos"))),
            .SlidingExpiration = True,
            .CookieHttpOnly = True,
            .CookieSecure = CookieSecureOption.SameAsRequest,
            .CookieManager = New SystemWebChunkingCookieManager()
        })

        ' "No hay cookie: ¿cómo averiguo quién eres?": Authorization Code + PKCE contra Keycloak
        app.UseOpenIdConnectAuthentication(New OpenIdConnectAuthenticationOptions With {
            .Authority = ConfigurationManager.AppSettings("oidc:Authority"),
            .ClientId = ConfigurationManager.AppSettings("oidc:ClientId"),
            .ClientSecret = ConfigurationManager.AppSettings("oidc:ClientSecret"),
            .RedirectUri = ConfigurationManager.AppSettings("oidc:RedirectUri"),
            .PostLogoutRedirectUri = ConfigurationManager.AppSettings("oidc:PostLogoutRedirectUri"),
            .RequireHttpsMetadata = Boolean.Parse(ConfigurationManager.AppSettings("oidc:RequireHttpsMetadata")),
            .ResponseType = OpenIdConnectResponseType.Code,
            .ResponseMode = OpenIdConnectResponseMode.Query,
            .RedeemCode = True,
            .UsePkce = True,
            .Scope = "openid profile email",
            .SaveTokens = True,
            .UseTokenLifetime = False,
            .TokenValidationParameters = New TokenValidationParameters With {
                .NameClaimType = "preferred_username"
            },
            .Notifications = New OpenIdConnectAuthenticationNotifications With {
                .RedirectToIdentityProvider = AddressOf AgregarIdTokenHintAlLogout
            },
            .CookieManager = New SameSiteLaxCookieManager()
        })
    End Sub

    ' Logout: id_token_hint para que Keycloak vuelva a post_logout_redirect_uri sin pedir confirmación
    Private Shared Async Function AgregarIdTokenHintAlLogout(
            n As RedirectToIdentityProviderNotification(Of OpenIdConnectMessage, OpenIdConnectAuthenticationOptions)) As Task
        If n.ProtocolMessage.RequestType <> OpenIdConnectRequestType.Logout Then
            Return
        End If

        n.ProtocolMessage.ClientId = n.Options.ClientId

        Dim sesion As AuthenticateResult = Await n.OwinContext.Authentication.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationType)

        Dim idToken As String = Nothing
        If sesion IsNot Nothing AndAlso sesion.Properties.Dictionary.TryGetValue(OpenIdConnectParameterNames.IdToken, idToken) Then
            n.ProtocolMessage.IdTokenHint = idToken
        End If
    End Function

End Class

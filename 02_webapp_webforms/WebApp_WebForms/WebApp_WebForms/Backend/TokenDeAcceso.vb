Imports System.Globalization
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.IdentityModel.Protocols.OpenIdConnect
Imports Microsoft.Owin
Imports Microsoft.Owin.Security.Cookies
Imports Newtonsoft.Json.Linq

' Access token del usuario para llamar a 05_backend.
' Startup guarda los tokens de Keycloak en la sesión (SaveTokens), que vive en IIS (SesionEnServidor).
' El access token dura minutos y la sesión una hora: cuando vence se renueva con el refresh token y se
' guarda una sesión nueva.
' Nothing = ya no hay forma de renovarlo (sesión de Keycloak cerrada o vencida) y hay que volver a
' hacer login.
Public Module TokenDeAcceso

    ' Renovar un poco antes de que venza, para que no expire en camino al backend
    Private ReadOnly Margen As TimeSpan = TimeSpan.FromSeconds(30)

    Private Const ClaveVence As String = "expires_at"

    Private ReadOnly Http As New HttpClient()

    ' Lee el token_endpoint del discovery de Keycloak y lo cachea
    Private ReadOnly Metadatos As New Microsoft.IdentityModel.Protocols.ConfigurationManager(Of OpenIdConnectConfiguration)(
        ConfigurationManager.AppSettings("oidc:Authority").TrimEnd("/"c) & "/.well-known/openid-configuration",
        New OpenIdConnectConfigurationRetriever(),
        New Microsoft.IdentityModel.Protocols.HttpDocumentRetriever() With {
            .RequireHttps = Boolean.Parse(ConfigurationManager.AppSettings("oidc:RequireHttpsMetadata"))
        })

    Public Async Function ObtenerAsync(contexto As IOwinContext) As Task(Of String)
        Dim sesion = Await contexto.Authentication.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationType)
        If sesion Is Nothing OrElse sesion.Identity Is Nothing Then
            Return Nothing
        End If

        Dim tokens = sesion.Properties.Dictionary

        Dim accessToken As String = Nothing
        If tokens.TryGetValue(OpenIdConnectParameterNames.AccessToken, accessToken) AndAlso Not Vencido(tokens) Then
            Return accessToken
        End If

        Dim refreshToken As String = Nothing
        If Not tokens.TryGetValue(OpenIdConnectParameterNames.RefreshToken, refreshToken) Then
            Return Nothing
        End If

        Dim nuevos = Await RenovarAsync(refreshToken)
        If nuevos Is Nothing Then
            Return Nothing
        End If

        Guardar(tokens, nuevos)

        ' Misma identidad, tokens nuevos: el middleware de cookies guarda el ticket en SesionEnServidor y
        ' reemite .AUTH.WEBFORMS con la llave nueva al responder
        sesion.Properties.IssuedUtc = Nothing
        sesion.Properties.ExpiresUtc = Nothing
        contexto.Authentication.SignIn(sesion.Properties, sesion.Identity)

        Return tokens(OpenIdConnectParameterNames.AccessToken)
    End Function

    ' Sin expires_at no se sabe: se usa el token y, si ya venció, el backend responde 401
    Private Function Vencido(tokens As IDictionary(Of String, String)) As Boolean
        Dim valor As String = Nothing
        Dim vence As DateTimeOffset
        If Not tokens.TryGetValue(ClaveVence, valor) OrElse
                Not DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, vence) Then
            Return False
        End If

        Return vence - Margen <= DateTimeOffset.UtcNow
    End Function

    ' grant_type=refresh_token contra Keycloak. Nothing si lo rechaza (refresh vencido o revocado).
    Private Async Function RenovarAsync(refreshToken As String) As Task(Of JObject)
        Dim configuracion = Await Metadatos.GetConfigurationAsync(CancellationToken.None)

        Dim cuerpo As New FormUrlEncodedContent(New Dictionary(Of String, String) From {
            {"grant_type", "refresh_token"},
            {"refresh_token", refreshToken},
            {"client_id", ConfigurationManager.AppSettings("oidc:ClientId")},
            {"client_secret", ConfigurationManager.AppSettings("oidc:ClientSecret")}
        })

        Using respuesta = Await Http.PostAsync(configuracion.TokenEndpoint, cuerpo)
            If Not respuesta.IsSuccessStatusCode Then
                Return Nothing
            End If

            Return JObject.Parse(Await respuesta.Content.ReadAsStringAsync())
        End Using
    End Function

    ' Mismas claves que usa SaveTokens de Katana, para que el logout siga encontrando el id_token
    Private Sub Guardar(tokens As IDictionary(Of String, String), nuevos As JObject)
        For Each nombre In {OpenIdConnectParameterNames.AccessToken,
                            OpenIdConnectParameterNames.RefreshToken,
                            OpenIdConnectParameterNames.IdToken}
            Dim valor = nuevos.Value(Of String)(nombre)
            If Not String.IsNullOrEmpty(valor) Then
                tokens(nombre) = valor
            End If
        Next

        Dim segundos = nuevos.Value(Of Integer?)(OpenIdConnectParameterNames.ExpiresIn)
        If segundos.HasValue Then
            tokens(ClaveVence) = DateTimeOffset.UtcNow.AddSeconds(segundos.Value).ToString("o", CultureInfo.InvariantCulture)
        Else
            tokens.Remove(ClaveVence)
        End If
    End Sub

End Module

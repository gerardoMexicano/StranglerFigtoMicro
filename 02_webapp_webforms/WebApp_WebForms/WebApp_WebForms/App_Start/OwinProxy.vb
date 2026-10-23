Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks
Imports Microsoft.Owin
Imports Microsoft.Owin.Host.SystemWeb
Imports Microsoft.Owin.Infrastructure
Imports Owin

' Mismo código que WebApp_MVC/App_Start/OwinProxy.cs.
Public Module OwinProxy

    ' Detrás de nginx + gateway, IIS ve http://192.168.50.243:8081/legacy/... pero el navegador ve el
    ' dominio público. Katana arma URLs absolutas y decide si la cookie es Secure con lo que ve IIS;
    ' esto le devuelve el esquema y el host originales. Solo se confía en las cabeceras si la petición
    ' viene de un proxy conocido: IIS también es alcanzable directo en la LAN.
    <Extension()>
    Public Sub UsarEncabezadosDelProxy(app As IAppBuilder, proxiesConfiables As String)
        Dim confiables As New HashSet(Of String)(
            If(proxiesConfiables, "").Split(New Char() {","c, ";"c, " "c}, StringSplitOptions.RemoveEmptyEntries))

        ' Sin proxy (Visual Studio / IIS Express): no hay nada que corregir
        If confiables.Count = 0 Then
            Return
        End If

        Dim manejador As Func(Of IOwinContext, Func(Of Task), Task) =
            Function(contexto, siguiente)
                If confiables.Contains(contexto.Request.RemoteIpAddress) Then
                    Dim esquema As String = PrimerValor(contexto.Request.Headers("X-Forwarded-Proto"))
                    Dim host As String = PrimerValor(contexto.Request.Headers("X-Forwarded-Host"))

                    If esquema IsNot Nothing Then
                        contexto.Request.Scheme = esquema
                    End If

                    If host IsNot Nothing Then
                        contexto.Request.Host = New HostString(host)
                    End If
                End If

                Return siguiente()
            End Function

        ' Llamada explícita: en VB, app.Use(lambda) elegiría IAppBuilder.Use(Object) y fallaría al arrancar
        AppBuilderUseExtensions.Use(app, manejador)
    End Sub

    ' Si pasó por varios proxies, el primero de la lista es el que vio el navegador
    Private Function PrimerValor(valor As String) As String
        If String.IsNullOrWhiteSpace(valor) Then
            Return Nothing
        End If

        Return valor.Split(","c)(0).Trim()
    End Function

End Module

' Katana crea la cookie OpenIdConnect.nonce con SameSite=None, y el navegador la descarta si no va por
' https: el nonce se pierde y el login falla al volver de Keycloak. Con ResponseMode=Query Keycloak
' regresa con un GET normal entre sitios, así que SameSite=Lax basta.
Public Class SameSiteLaxCookieManager
    Implements ICookieManager

    Private ReadOnly interno As ICookieManager = New SystemWebCookieManager()

    Public Function GetRequestCookie(context As IOwinContext, key As String) As String Implements ICookieManager.GetRequestCookie
        Return interno.GetRequestCookie(context, key)
    End Function

    Public Sub AppendResponseCookie(context As IOwinContext, key As String, value As String, options As CookieOptions) Implements ICookieManager.AppendResponseCookie
        interno.AppendResponseCookie(context, key, value, ALax(options))
    End Sub

    Public Sub DeleteCookie(context As IOwinContext, key As String, options As CookieOptions) Implements ICookieManager.DeleteCookie
        interno.DeleteCookie(context, key, ALax(options))
    End Sub

    Private Shared Function ALax(options As CookieOptions) As CookieOptions
        If options.SameSite.HasValue AndAlso options.SameSite.Value = SameSiteMode.None Then
            options.SameSite = SameSiteMode.Lax
        End If

        Return options
    End Function

End Class

Imports System.Threading.Tasks
Imports System.Web.Caching
Imports Microsoft.Owin.Security
Imports Microsoft.Owin.Security.Cookies

' Mismo código que WebApp_MVC/App_Start/SesionEnServidor.cs.
' La sesión (claims + access, refresh e id token de Keycloak por SaveTokens) se queda en la memoria
' de IIS y la cookie .AUTH.WEBFORMS solo lleva su llave cifrada (unos cientos de bytes). Con todo
' adentro la cookie ocupaba varios KB: el navegador la descartaba (de ahí los trozos), nginx daba 502
' al responder el login y, con las cookies de MVC y WebForms juntas, el encabezado Cookie puede rebasar
' el límite de nginx (8 KB por línea: 400 "Request Header Or Cookie Too Large").
' Si IIS recicla el app pool se pierden las sesiones: OIDC vuelve a Keycloak y, con su sesión
' abierta, el usuario entra sin escribir la contraseña.
Public Class SesionEnServidor
    Implements IAuthenticationSessionStore

    Private Const Prefijo As String = "sesion-owin:"

    ' Respaldo por si el ticket no trae vencimiento (el middleware de cookies siempre lo pone)
    Private ReadOnly _duracion As TimeSpan

    Public Sub New(duracion As TimeSpan)
        _duracion = duracion
    End Sub

    Public Async Function StoreAsync(ticket As AuthenticationTicket) As Task(Of String) Implements IAuthenticationSessionStore.StoreAsync
        Dim llave As String = Guid.NewGuid().ToString("N")
        Await RenewAsync(llave, ticket)
        Return llave
    End Function

    ' Vence junto con la cookie; NotRemovable para que IIS no la tire por presión de memoria
    Public Function RenewAsync(key As String, ticket As AuthenticationTicket) As Task Implements IAuthenticationSessionStore.RenewAsync
        Dim vence As DateTimeOffset = If(ticket.Properties.ExpiresUtc, DateTimeOffset.UtcNow.Add(_duracion))
        HttpRuntime.Cache.Insert(Prefijo & key, ticket, Nothing, vence.UtcDateTime, Cache.NoSlidingExpiration,
                                 CacheItemPriority.NotRemovable, Nothing)
        Return Task.FromResult(0)
    End Function

    ' Nothing = la sesión ya no existe (venció o se recicló el app pool): el usuario queda anónimo
    Public Function RetrieveAsync(key As String) As Task(Of AuthenticationTicket) Implements IAuthenticationSessionStore.RetrieveAsync
        Return Task.FromResult(TryCast(HttpRuntime.Cache.Get(Prefijo & key), AuthenticationTicket))
    End Function

    Public Function RemoveAsync(key As String) As Task Implements IAuthenticationSessionStore.RemoveAsync
        HttpRuntime.Cache.Remove(Prefijo & key)
        Return Task.FromResult(0)
    End Function

End Class

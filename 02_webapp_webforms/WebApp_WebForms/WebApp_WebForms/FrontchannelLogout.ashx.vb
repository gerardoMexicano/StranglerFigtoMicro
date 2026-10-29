Imports System.Security.Claims
Imports Microsoft.Owin.Security.Cookies

' GET FrontchannelLogout.ashx?iss=...&sid=...
' La llama Keycloak en un iframe cuando la sesión SSO se cierra desde otra app (ver App_Start/CierreDesdeKeycloak.vb).
' Solo borra la cookie de esta app; siempre responde 200 vacío, sin caché.
' Anónima en el Web.config (<location path="FrontchannelLogout.ashx">): sin sesión no hay nada que cerrar.
Public Class FrontchannelLogout
    Implements IHttpHandler

    Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
        If CierreDesdeKeycloak.EsDeEstaSesion(TryCast(context.User, ClaimsPrincipal),
                context.Request.QueryString("iss"), context.Request.QueryString("sid"),
                ConfigurationManager.AppSettings("oidc:Authority")) Then
            context.GetOwinContext().Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType)
        End If

        context.Response.Cache.SetCacheability(HttpCacheability.NoCache)
        context.Response.Cache.SetNoStore()
    End Sub

    Public ReadOnly Property IsReusable As Boolean Implements IHttpHandler.IsReusable
        Get
            Return True
        End Get
    End Property

End Class

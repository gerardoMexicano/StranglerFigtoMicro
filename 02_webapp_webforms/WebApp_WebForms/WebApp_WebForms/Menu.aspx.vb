Imports Microsoft.Owin.Security.Cookies
Imports Microsoft.Owin.Security.OpenIdConnect

Public Class Menu
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Si no hay sesión activa, redirige al login
        If Not Request.IsAuthenticated Then
            Response.Redirect("Login.aspx")
            Return
        End If

        ' Para mostrar el nombre del usuario
        lblUsuario.Text = User.Identity.Name
    End Sub

    ' Cierra las DOS sesiones: la cookie de esta app y la de Keycloak.
    ' Si solo se borrara la cookie, el siguiente clic volvería a entrar por SSO sin pedir contraseña.
    Protected Sub btnCerrarSesion_Click(ByVal sender As Object, ByVal e As System.EventArgs)
        Session.Clear()
        Session.Abandon()

        ' Eliminar cookie ASP.NET_SessionId
        If Request.Cookies("ASP.NET_SessionId") IsNot Nothing Then
            Dim sessionCookie As New HttpCookie("ASP.NET_SessionId")
            sessionCookie.Expires = DateTime.Now.AddDays(-1)
            sessionCookie.Value = Nothing
            Response.Cookies.Add(sessionCookie)
        End If

        ' El middleware OIDC convierte esto en el redirect al end_session_endpoint de Keycloak,
        ' que al terminar regresa a oidc:PostLogoutRedirectUri (la página de inicio)
        Context.GetOwinContext().Authentication.SignOut(
            OpenIdConnectAuthenticationDefaults.AuthenticationType,
            CookieAuthenticationDefaults.AuthenticationType)

        Response.SuppressContent = True
        Context.ApplicationInstance.CompleteRequest()
    End Sub

End Class

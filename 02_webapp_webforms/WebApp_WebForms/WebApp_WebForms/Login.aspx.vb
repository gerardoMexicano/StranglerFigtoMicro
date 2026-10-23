Imports Microsoft.Owin.Security
Imports Microsoft.Owin.Security.OpenIdConnect

Public Class Login
    Inherits System.Web.UI.Page

    ' Ya no pide contraseña: manda al usuario a Keycloak y al volver lo deja en el menú
    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        Dim destino As String = ResolveUrl("~/Menu.aspx")

        If Request.IsAuthenticated Then
            Response.Redirect(destino)
            Return
        End If

        ' Challenge deja la respuesta en 401; el middleware OIDC la convierte en el redirect a Keycloak
        Context.GetOwinContext().Authentication.Challenge(
            New AuthenticationProperties With {.RedirectUri = destino},
            OpenIdConnectAuthenticationDefaults.AuthenticationType)

        Response.SuppressContent = True
        Context.ApplicationInstance.CompleteRequest()
    End Sub

End Class

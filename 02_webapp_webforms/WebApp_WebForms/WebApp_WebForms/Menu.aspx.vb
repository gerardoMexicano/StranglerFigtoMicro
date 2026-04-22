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

    Protected Sub btnCerrarSesion_Click(ByVal sender As Object, ByVal e As System.EventArgs)
        Session.Clear()
        Session.Abandon()
        FormsAuthentication.SignOut()
        ' 3. Eliminar cookie .AUTH manualmente
        If Request.Cookies(".AUTH") IsNot Nothing Then
            Dim authCookie As New HttpCookie(".AUTH")
            authCookie.Expires = DateTime.Now.AddDays(-1)
            authCookie.Value = Nothing
            Response.Cookies.Add(authCookie)
        End If

        ' 4. Eliminar cookie ASP.NET_SessionId
        If Request.Cookies("ASP.NET_SessionId") IsNot Nothing Then
            Dim sessionCookie As New HttpCookie("ASP.NET_SessionId")
            sessionCookie.Expires = DateTime.Now.AddDays(-1)
            sessionCookie.Value = Nothing
            Response.Cookies.Add(sessionCookie)
        End If
        Response.Redirect("Default.aspx")
    End Sub

End Class

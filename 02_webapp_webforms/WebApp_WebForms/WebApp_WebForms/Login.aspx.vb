Imports System.Data.SqlClient
Imports System.Configuration
Imports System.Web.Security

Public Class Login
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Si ya hay sesión activa redirige al menú
        If Session("UsuarioLogueado") IsNot Nothing Then
            Response.Redirect("Menu.aspx")
        End If
    End Sub

    Protected Sub btnLogin_Click(ByVal sender As Object, ByVal e As System.EventArgs)
        Dim usuario As String = txtUsuario.Text.Trim()
        Dim password As String = txtPassword.Text.Trim()

        If String.IsNullOrEmpty(usuario) OrElse String.IsNullOrEmpty(password) Then
            MostrarError("Por favor ingresa usuario y contraseña.")
            Return
        End If

        If ValidarCredenciales(usuario, password) Then
            FormsAuthentication.SetAuthCookie(usuario, False)
            Response.Redirect("Menu.aspx")
        Else
            MostrarError("Usuario o contraseña incorrectos.")
        End If
    End Sub


    Private Function ValidarCredenciales(usuario As String, password As String) As Boolean
        Dim conexionString As String = ConfigurationManager.ConnectionStrings("ConexionDB").ConnectionString

        Using conexion As New SqlConnection(conexionString)
            Dim query As String = "SELECT COUNT(*) FROM Usuarios WHERE username = @usuario AND password = @password"

            Using comando As New SqlCommand(query, conexion)
                comando.Parameters.AddWithValue("@usuario", usuario)
                comando.Parameters.AddWithValue("@password", password)

                conexion.Open()
                Dim resultado As Integer = Convert.ToInt32(comando.ExecuteScalar())

                Return resultado > 0
            End Using
        End Using
    End Function

    Private Sub MostrarError(mensaje As String)
        lblError.Text = mensaje
        lblError.Visible = True
    End Sub

End Class

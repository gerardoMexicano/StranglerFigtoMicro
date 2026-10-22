Imports System.Data.SqlClient
Imports System.Configuration
Imports System.Security.Cryptography
Imports System.Text
Imports System.Web.Security
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

        ' El formulario ya no se muestra: no tiene caso generar el HTML de la página
        Response.SuppressContent = True
        Context.ApplicationInstance.CompleteRequest()
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


    ' Hash: SHA-512( salt + UTF-16LE(password) ), igual que sqlserver/db/03_usuarios_hash_expand.sql
    Private Function ValidarCredenciales(usuario As String, password As String) As Boolean
        Dim conexionString As String = ConfigurationManager.ConnectionStrings("ConexionDB").ConnectionString

        Using conexion As New SqlConnection(conexionString)
            conexion.Open()

            Dim id As Integer
            Dim salt As Byte()
            Dim hashGuardado As Byte()

            Dim query As String = "SELECT id, password_salt, password_hash FROM usuarios WHERE username = @usuario AND activo = 1"

            Using comando As New SqlCommand(query, conexion)
                comando.Parameters.AddWithValue("@usuario", usuario)

                Using lector As SqlDataReader = comando.ExecuteReader()
                    If Not lector.Read() OrElse lector.IsDBNull(1) OrElse lector.IsDBNull(2) Then
                        Return False
                    End If

                    id = lector.GetInt32(0)
                    salt = CType(lector(1), Byte())
                    hashGuardado = CType(lector(2), Byte())
                End Using
            End Using

            If Not SonIguales(CalcularHash(salt, password), hashGuardado) Then
                Return False
            End If

            Using comando As New SqlCommand("UPDATE usuarios SET ultimo_login = SYSDATETIME() WHERE id = @id", conexion)
                comando.Parameters.AddWithValue("@id", id)
                comando.ExecuteNonQuery()
            End Using

            Return True
        End Using
    End Function

    Private Shared Function CalcularHash(salt As Byte(), password As String) As Byte()
        Dim passwordBytes As Byte() = Encoding.Unicode.GetBytes(password)
        Dim entrada(salt.Length + passwordBytes.Length - 1) As Byte
        System.Buffer.BlockCopy(salt, 0, entrada, 0, salt.Length)
        System.Buffer.BlockCopy(passwordBytes, 0, entrada, salt.Length, passwordBytes.Length)

        Using sha As SHA512 = SHA512.Create()
            Return sha.ComputeHash(entrada)
        End Using
    End Function

    ' Comparación en tiempo constante (.NET Framework no trae CryptographicOperations.FixedTimeEquals)
    Private Shared Function SonIguales(a As Byte(), b As Byte()) As Boolean
        If a.Length <> b.Length Then
            Return False
        End If

        Dim diferencia As Integer = 0
        For i As Integer = 0 To a.Length - 1
            diferencia = diferencia Or (a(i) Xor b(i))
        Next

        Return diferencia = 0
    End Function

    Private Sub MostrarError(mensaje As String)
        lblError.Text = mensaje
        lblError.Visible = True
    End Sub

End Class

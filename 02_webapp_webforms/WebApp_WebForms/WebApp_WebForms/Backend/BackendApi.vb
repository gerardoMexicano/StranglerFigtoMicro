Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Threading.Tasks
Imports Microsoft.Owin
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

' Llamadas a 05_backend (/api por nginx + gateway) con el access token del usuario.
' Se llama desde el servidor (code-behind): el token nunca llega al navegador.
Public Module BackendApi

    Private ReadOnly Http As HttpClient = CrearCliente()

    Private Function CrearCliente() As HttpClient
        Return New HttpClient() With {
            .BaseAddress = New Uri(ConfigurationManager.AppSettings("backend:BaseUrl").TrimEnd("/"c) & "/"),
            .Timeout = TimeSpan.FromSeconds(Integer.Parse(ConfigurationManager.AppSettings("backend:TimeoutSegundos")))
        }
    End Function

    ' ruta es relativa a backend:BaseUrl, p. ej. "sales/orders?page=1"
    Public Async Function ObtenerAsync(Of T)(contexto As IOwinContext, ruta As String) As Task(Of T)
        Dim token = Await TokenDeAcceso.ObtenerAsync(contexto)
        If token Is Nothing Then
            Throw New BackendSinSesionException()
        End If

        Using peticion As New HttpRequestMessage(HttpMethod.Get, ruta)
            peticion.Headers.Authorization = New AuthenticationHeaderValue("Bearer", token)

            Using respuesta = Await Http.SendAsync(peticion)
                Dim cuerpo = Await respuesta.Content.ReadAsStringAsync()

                If respuesta.StatusCode = HttpStatusCode.Unauthorized Then
                    Throw New BackendSinSesionException()
                End If

                If Not respuesta.IsSuccessStatusCode Then
                    Throw New BackendApiException(respuesta.StatusCode, DetalleDelProblema(cuerpo, respuesta.ReasonPhrase))
                End If

                Return JsonConvert.DeserializeObject(Of T)(cuerpo)
            End Using
        End Using
    End Function

    ' El backend responde errores como ProblemDetails (RFC 7807): detail, o si no title
    Private Function DetalleDelProblema(cuerpo As String, porDefecto As String) As String
        Try
            Dim problema = JObject.Parse(cuerpo)
            Return If(problema.Value(Of String)("detail"), If(problema.Value(Of String)("title"), porDefecto))
        Catch ex As JsonReaderException
            Return porDefecto
        End Try
    End Function

End Module

' PagedResult<T> del backend. Items como DataTable para enlazarlo al GridView igual que antes.
Public Class PaginaDelBackend
    Public Property Items As DataTable
    Public Property Page As Integer
    Public Property PageSize As Integer
    Public Property TotalCount As Integer
End Class

' No hay token válido o el backend lo rechazó (401): hay que volver a hacer login
Public Class BackendSinSesionException
    Inherits Exception

    Public Sub New()
        MyBase.New("La sesión con el backend venció.")
    End Sub
End Class

Public Class BackendApiException
    Inherits Exception

    Public ReadOnly Property Estado As HttpStatusCode

    Public Sub New(estado As HttpStatusCode, detalle As String)
        MyBase.New(detalle)
        Me.Estado = estado
    End Sub
End Class

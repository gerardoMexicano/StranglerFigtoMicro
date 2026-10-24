Imports System.Globalization
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

    ' Decimales como Decimal (no Double), igual que las columnas money/decimal de SQL
    Private ReadOnly Json As New JsonSerializerSettings With {.FloatParseHandling = FloatParseHandling.Decimal}

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

                Return JsonConvert.DeserializeObject(Of T)(cuerpo, Json)
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

' PagedResult<T> del backend
Public Class PaginaDelBackend
    Public Property Items As JArray
    Public Property Page As Integer
    Public Property PageSize As Integer
    Public Property TotalCount As Integer

    ' Items como DataTable para enlazarlo al GridView igual que el resultado de SQL: columnas en
    ' PascalCase (el JSON viene en camelCase) y con el tipo de sus valores. No se deja a
    ' JsonConvert armar la tabla porque toma el tipo de la primera fila: un null o un entero ahí
    ' rompe las filas siguientes. Sin filas no hay columnas.
    Public Function ItemsComoTabla() As DataTable
        Dim tabla As New DataTable()
        If Items Is Nothing OrElse Items.Count = 0 Then
            Return tabla
        End If

        Dim filas = Items.OfType(Of JObject)().ToList()
        Dim nombres = filas(0).Properties().Select(Function(p) p.Name).ToList()

        For Each nombre In nombres
            tabla.Columns.Add(Char.ToUpperInvariant(nombre(0)) & nombre.Substring(1), TipoDeColumna(filas, nombre))
        Next

        For Each fila In filas
            Dim valores(nombres.Count - 1) As Object
            For i = 0 To nombres.Count - 1
                Dim valor = TryCast(fila(nombres(i)), JValue)
                valores(i) = If(valor Is Nothing OrElse valor.Value Is Nothing,
                                DBNull.Value,
                                Convert.ChangeType(valor.Value, tabla.Columns(i).DataType, CultureInfo.InvariantCulture))
            Next
            tabla.Rows.Add(valores)
        Next

        Return tabla
    End Function

    ' El tipo de los valores no nulos; enteros mezclados con decimales -> Decimal; otra mezcla -> String
    Private Shared Function TipoDeColumna(filas As List(Of JObject), nombre As String) As Type
        Dim tipos = filas.
            Select(Function(f) TryCast(f(nombre), JValue)).
            Where(Function(v) v IsNot Nothing AndAlso v.Value IsNot Nothing).
            Select(Function(v) v.Value.GetType()).
            Distinct().
            ToList()

        If tipos.Count = 1 Then
            Return tipos(0)
        End If

        If tipos.Count = 2 AndAlso tipos.Contains(GetType(Long)) AndAlso tipos.Contains(GetType(Decimal)) Then
            Return GetType(Decimal)
        End If

        Return GetType(String)
    End Function
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

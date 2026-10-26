Imports System.Data
Imports System.Data.SqlClient
Imports System.Configuration
Imports System.Globalization
Imports System.Net.Http
Imports System.Threading.Tasks
Imports Microsoft.Owin.Security.Cookies

Public Class LEgFrontNEWBACK
    Inherits System.Web.UI.Page

    ' Interruptor del Strangler Fig (Web.config backend:LEgFrontNEWBACK):
    ' "api" lee de 05_backend (/api/sales/*), "sql" consulta WideWorldImporters directo como antes
    Private ReadOnly UsarApi As Boolean =
        String.Equals(ConfigurationManager.AppSettings("backend:LEgFrontNEWBACK"), "api", StringComparison.OrdinalIgnoreCase)

    Protected Sub Page_Init(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Init
        ' Con la API cada página del grid es una llamada que trae solo las filas que se muestran
        For Each grid In {gvCustomerTransactions, gvInvoiceLines, gvInvoices, gvOrderLines, gvOrders}
            grid.AllowCustomPaging = UsarApi
        Next
    End Sub

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        lblOrigen.Text = If(UsarApi, "backend nuevo (/api/sales)", "SQL directo (WideWorldImporters)")

        If Not IsPostBack Then
            txtFechaInicio.Text = "2013-01-01"
            txtFechaFinal.Text = "2016-12-31"
        End If
    End Sub

    Protected Sub btnLoad1_Click(sender As Object, e As EventArgs)
        CargarCustomerTransactions()
    End Sub

    Protected Sub btnLoad2_Click(sender As Object, e As EventArgs)
        CargarInvoiceLines()
    End Sub

    Protected Sub btnLoad3_Click(sender As Object, e As EventArgs)
        CargarInvoices()
    End Sub

    Protected Sub btnLoad4_Click(sender As Object, e As EventArgs)
        CargarOrderLines()
    End Sub

    Protected Sub btnLoad5_Click(sender As Object, e As EventArgs)
        CargarOrders()
    End Sub

    Protected Sub gv1_PageIndexChanging(sender As Object, e As GridViewPageEventArgs)
        gvCustomerTransactions.PageIndex = e.NewPageIndex
        CargarCustomerTransactions()
    End Sub

    Protected Sub gv2_PageIndexChanging(sender As Object, e As GridViewPageEventArgs)
        gvInvoiceLines.PageIndex = e.NewPageIndex
        CargarInvoiceLines()
    End Sub

    Protected Sub gv3_PageIndexChanging(sender As Object, e As GridViewPageEventArgs)
        gvInvoices.PageIndex = e.NewPageIndex
        CargarInvoices()
    End Sub

    Protected Sub gv4_PageIndexChanging(sender As Object, e As GridViewPageEventArgs)
        gvOrderLines.PageIndex = e.NewPageIndex
        CargarOrderLines()
    End Sub

    Protected Sub gv5_PageIndexChanging(sender As Object, e As GridViewPageEventArgs)
        gvOrders.PageIndex = e.NewPageIndex
        CargarOrders()
    End Sub

    Private Function GetDates(ByRef startD As DateTime, ByRef endD As DateTime) As Boolean
        Dim fechaInicio As String = txtFechaInicio.Text
        Dim fechaFinal As String = txtFechaFinal.Text

        If String.IsNullOrEmpty(fechaInicio) OrElse String.IsNullOrEmpty(fechaFinal) Then
            Return False
        End If

        If Not DateTime.TryParse(fechaInicio, startD) OrElse Not DateTime.TryParse(fechaFinal, endD) Then
            Return False
        End If

        Return True
    End Function

    Private Sub CargarCustomerTransactions()
        Cargar(gvCustomerTransactions, "sales/customer-transactions",
               "SELECT [CustomerTransactionID], [CustomerID], [TransactionTypeID], [InvoiceID], [PaymentMethodID], [TransactionDate], [AmountExcludingTax], [TaxAmount], [TransactionAmount], [OutstandingBalance], [FinalizationDate], [IsFinalized], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[CustomerTransactions] WHERE [TransactionDate] >= @start AND [TransactionDate] <= @end")
    End Sub

    Private Sub CargarInvoiceLines()
        Cargar(gvInvoiceLines, "sales/invoice-lines",
               "SELECT [InvoiceLineID], [InvoiceID], [StockItemID], [Description], [PackageTypeID], [Quantity], [UnitPrice], [TaxRate], [TaxAmount], [LineProfit], [ExtendedPrice], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[InvoiceLines] WHERE [LastEditedWhen] >= @start AND [LastEditedWhen] <= @end")
    End Sub

    Private Sub CargarInvoices()
        Cargar(gvInvoices, "sales/invoices",
               "SELECT [InvoiceID], [CustomerID], [BillToCustomerID], [OrderID], [DeliveryMethodID], [ContactPersonID], [AccountsPersonID], [SalespersonPersonID], [PackedByPersonID], [InvoiceDate], [CustomerPurchaseOrderNumber], [IsCreditNote], [CreditNoteReason], [Comments], [DeliveryInstructions], [InternalComments], [TotalDryItems], [TotalChillerItems], [DeliveryRun], [RunPosition], [ReturnedDeliveryData], [ConfirmedDeliveryTime], [ConfirmedReceivedBy], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[Invoices] WHERE [InvoiceDate] >= @start AND [InvoiceDate] <= @end")
    End Sub

    Private Sub CargarOrderLines()
        Cargar(gvOrderLines, "sales/order-lines",
               "SELECT [OrderLineID], [OrderID], [StockItemID], [Description], [PackageTypeID], [Quantity], [UnitPrice], [TaxRate], [PickedQuantity], [PickingCompletedWhen], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[OrderLines] WHERE [LastEditedWhen] >= @start AND [LastEditedWhen] <= @end")
    End Sub

    Private Sub CargarOrders()
        Cargar(gvOrders, "sales/orders",
               "SELECT [OrderID], [CustomerID], [SalespersonPersonID], [PickedByPersonID], [ContactPersonID], [BackorderOrderID], [OrderDate], [ExpectedDeliveryDate], [CustomerPurchaseOrderNumber], [IsUndersupplyBackordered], [Comments], [DeliveryInstructions], [InternalComments], [PickingCompletedWhen], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[Orders] WHERE [OrderDate] >= @start AND [OrderDate] <= @end")
    End Sub

    Private Sub Cargar(grid As GridView, rutaApi As String, consultaSql As String)
        Dim startD As DateTime, endD As DateTime
        If Not GetDates(startD, endD) Then Return

        If UsarApi Then
            ' Page Async="true": la llamada HTTP no bloquea un hilo de IIS mientras responde el backend
            RegisterAsyncTask(New PageAsyncTask(Function() CargarDesdeApiAsync(grid, rutaApi, startD, endD)))
        Else
            CargarDesdeSql(grid, consultaSql, startD, endD)
        End If
    End Sub

    Private Async Function CargarDesdeApiAsync(grid As GridView, ruta As String, startD As DateTime, endD As DateTime) As Task
        ' El backend ya toma la fecha final completa (hasta las 23:59:59.9999999), igual que GetData
        Dim url As String = String.Format(CultureInfo.InvariantCulture,
            "{0}?startDate={1:yyyy-MM-dd}&endDate={2:yyyy-MM-dd}&page={3}&pageSize={4}",
            ruta, startD, endD, grid.PageIndex + 1, grid.PageSize)

        Try
            Dim pagina = Await BackendApi.ObtenerAsync(Of PaginaDelBackend)(Context.GetOwinContext(), url)
            Dim tabla = pagina.ItemsComoTabla()

            If tabla.Rows.Count = 0 Then
                MostrarMensaje(grid, "Sin registros en el rango de fechas.")
                Return
            End If

            grid.VirtualItemCount = pagina.TotalCount
            grid.DataSource = tabla
            grid.DataBind()
        Catch ex As BackendSinSesionException
            VolverAIniciarSesion()
        Catch ex As BackendApiException
            MostrarMensaje(grid, "No se pudo consultar el backend: " & ex.Message)
        Catch ex As HttpRequestException
            MostrarMensaje(grid, "No se pudo conectar con el backend.")
        Catch ex As TaskCanceledException
            MostrarMensaje(grid, "El backend tardó demasiado en responder.")
        End Try
    End Function

    ' Una fila con el aviso en lugar del grid vacío (sin columnas el GridView no puede autogenerarlas)
    Private Sub MostrarMensaje(grid As GridView, mensaje As String)
        Dim tabla As New DataTable()
        tabla.Columns.Add("Mensaje", GetType(String))
        tabla.Rows.Add(mensaje)

        grid.PageIndex = 0
        grid.VirtualItemCount = 1
        grid.DataSource = tabla
        grid.DataBind()
    End Sub

    ' Keycloak ya no renueva el token: se cierra solo la sesión local y se recarga la página.
    ' Web.config exige sesión, OIDC manda a Keycloak y, si su sesión sigue abierta, vuelve sin pedir
    ' contraseña. En un postback del UpdatePanel, ScriptManager convierte el redirect en navegación.
    Private Sub VolverAIniciarSesion()
        Context.GetOwinContext().Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType)
        Response.Redirect(Request.RawUrl, False)
        Context.ApplicationInstance.CompleteRequest()
    End Sub

    Private Sub CargarDesdeSql(grid As GridView, consultaSql As String, startD As DateTime, endD As DateTime)
        Dim connStr As String = ConfigurationManager.ConnectionStrings("WideWorldImporters").ConnectionString
        Using conn As New SqlConnection(connStr)
            conn.Open()
            grid.DataSource = GetData(conn, consultaSql, startD, endD)
            grid.DataBind()
        End Using
    End Sub

    Private Function GetData(conn As SqlConnection, query As String, startD As DateTime, endD As DateTime) As DataTable
        Dim dt As New DataTable()
        Using cmd As New SqlCommand(query, conn)
            cmd.CommandTimeout = 300 ' 5 minutos para consultas pesadas
            cmd.Parameters.AddWithValue("@start", startD)
            cmd.Parameters.AddWithValue("@end", endD.AddDays(1).AddTicks(-1))
            Using adapter As New SqlDataAdapter(cmd)
                adapter.Fill(dt)
            End Using
        End Using
        Return dt
    End Function

End Class

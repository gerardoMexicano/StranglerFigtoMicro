Imports System.Data
Imports System.Data.SqlClient
Imports System.Configuration

Public Class LEgFrontNEWBACK
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
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
        Dim startD As DateTime, endD As DateTime
        If Not GetDates(startD, endD) Then Return

        Dim connStr As String = ConfigurationManager.ConnectionStrings("WideWorldImporters").ConnectionString
        Using conn As New SqlConnection(connStr)
            conn.Open()
            Dim q1 As String = "SELECT [CustomerTransactionID], [CustomerID], [TransactionTypeID], [InvoiceID], [PaymentMethodID], [TransactionDate], [AmountExcludingTax], [TaxAmount], [TransactionAmount], [OutstandingBalance], [FinalizationDate], [IsFinalized], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[CustomerTransactions] WHERE [TransactionDate] >= @start AND [TransactionDate] <= @end"
            gvCustomerTransactions.DataSource = GetData(conn, q1, startD, endD)
            gvCustomerTransactions.DataBind()
        End Using
    End Sub

    Private Sub CargarInvoiceLines()
        Dim startD As DateTime, endD As DateTime
        If Not GetDates(startD, endD) Then Return

        Dim connStr As String = ConfigurationManager.ConnectionStrings("WideWorldImporters").ConnectionString
        Using conn As New SqlConnection(connStr)
            conn.Open()
            Dim q2 As String = "SELECT [InvoiceLineID], [InvoiceID], [StockItemID], [Description], [PackageTypeID], [Quantity], [UnitPrice], [TaxRate], [TaxAmount], [LineProfit], [ExtendedPrice], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[InvoiceLines] WHERE [LastEditedWhen] >= @start AND [LastEditedWhen] <= @end"
            gvInvoiceLines.DataSource = GetData(conn, q2, startD, endD)
            gvInvoiceLines.DataBind()
        End Using
    End Sub

    Private Sub CargarInvoices()
        Dim startD As DateTime, endD As DateTime
        If Not GetDates(startD, endD) Then Return

        Dim connStr As String = ConfigurationManager.ConnectionStrings("WideWorldImporters").ConnectionString
        Using conn As New SqlConnection(connStr)
            conn.Open()
            Dim q3 As String = "SELECT [InvoiceID], [CustomerID], [BillToCustomerID], [OrderID], [DeliveryMethodID], [ContactPersonID], [AccountsPersonID], [SalespersonPersonID], [PackedByPersonID], [InvoiceDate], [CustomerPurchaseOrderNumber], [IsCreditNote], [CreditNoteReason], [Comments], [DeliveryInstructions], [InternalComments], [TotalDryItems], [TotalChillerItems], [DeliveryRun], [RunPosition], [ReturnedDeliveryData], [ConfirmedDeliveryTime], [ConfirmedReceivedBy], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[Invoices] WHERE [InvoiceDate] >= @start AND [InvoiceDate] <= @end"
            gvInvoices.DataSource = GetData(conn, q3, startD, endD)
            gvInvoices.DataBind()
        End Using
    End Sub

    Private Sub CargarOrderLines()
        Dim startD As DateTime, endD As DateTime
        If Not GetDates(startD, endD) Then Return

        Dim connStr As String = ConfigurationManager.ConnectionStrings("WideWorldImporters").ConnectionString
        Using conn As New SqlConnection(connStr)
            conn.Open()
            Dim q4 As String = "SELECT [OrderLineID], [OrderID], [StockItemID], [Description], [PackageTypeID], [Quantity], [UnitPrice], [TaxRate], [PickedQuantity], [PickingCompletedWhen], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[OrderLines] WHERE [LastEditedWhen] >= @start AND [LastEditedWhen] <= @end"
            gvOrderLines.DataSource = GetData(conn, q4, startD, endD)
            gvOrderLines.DataBind()
        End Using
    End Sub

    Private Sub CargarOrders()
        Dim startD As DateTime, endD As DateTime
        If Not GetDates(startD, endD) Then Return

        Dim connStr As String = ConfigurationManager.ConnectionStrings("WideWorldImporters").ConnectionString
        Using conn As New SqlConnection(connStr)
            conn.Open()
            Dim q5 As String = "SELECT [OrderID], [CustomerID], [SalespersonPersonID], [PickedByPersonID], [ContactPersonID], [BackorderOrderID], [OrderDate], [ExpectedDeliveryDate], [CustomerPurchaseOrderNumber], [IsUndersupplyBackordered], [Comments], [DeliveryInstructions], [InternalComments], [PickingCompletedWhen], [LastEditedBy], [LastEditedWhen] FROM [WideWorldImporters].[Sales].[Orders] WHERE [OrderDate] >= @start AND [OrderDate] <= @end"
            gvOrders.DataSource = GetData(conn, q5, startD, endD)
            gvOrders.DataBind()
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
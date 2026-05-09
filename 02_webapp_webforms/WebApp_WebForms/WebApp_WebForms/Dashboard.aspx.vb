Imports System.Data
Imports System.Data.SqlClient
Imports System.Configuration
Imports Newtonsoft.Json

Public Class Dashboard
    Inherits System.Web.UI.Page

    ' ─────────────────────────────────────────────
    '  Connection string helper
    ' ─────────────────────────────────────────────
    Private ReadOnly Property ConnStr As String
        Get
            Return ConfigurationManager.ConnectionStrings("WideWorldImporters").ConnectionString
        End Get
    End Property

    ' ─────────────────────────────────────────────
    '  Page Load
    ' ─────────────────────────────────────────────
    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        If Not IsPostBack Then
            ' Defaults: 1 Enero del año actual → hoy
            Dim hoy As Date = Date.Today
            Dim inicio As New Date(hoy.Year, 1, 1)
            txtFechaIni.Value = inicio.ToString("yyyy-MM-dd")
            txtFechaFin.Value = hoy.ToString("yyyy-MM-dd")
        End If
        CargarDashboard()
    End Sub

    Protected Sub btnFiltrar_Click(ByVal sender As Object, ByVal e As EventArgs)
        CargarDashboard()
    End Sub

    ' ─────────────────────────────────────────────
    '  Orquestador principal
    ' ─────────────────────────────────────────────
    Private Sub CargarDashboard()
        Dim fechaIni As Date
        Dim fechaFin As Date

        If Not Date.TryParse(txtFechaIni.Value, fechaIni) Then fechaIni = New Date(Date.Today.Year, 1, 1)
        If Not Date.TryParse(txtFechaFin.Value, fechaFin) Then fechaFin = Date.Today

        Try
            EmitirScript("dashRevenueCosto", CargarRevenueVsCosto(fechaIni, fechaFin))
            EmitirScript("dashTop10", CargarTop10Clientes(fechaIni, fechaFin))
            EmitirScript("dashCategorias", CargarMargenCategoria(fechaIni, fechaFin))
            EmitirScript("dashVendedores", CargarEficienciaVendedor(fechaIni, fechaFin))
            CargarKPIs(fechaIni, fechaFin)
        Catch ex As Exception
            EmitirScript("dashError", New With {.error = ex.Message})
        End Try
    End Sub

    ' ─────────────────────────────────────────────
    '  Visual 1 — Revenue vs Costo vs Margen mensual
    ' ─────────────────────────────────────────────
    Private Function CargarRevenueVsCosto(fechaIni As Date, fechaFin As Date) As Object
        Dim sql As String = "
            SELECT
                YEAR(o.OrderDate)  AS Anio,
                MONTH(o.OrderDate) AS Mes,
                DATENAME(MONTH, o.OrderDate) AS NombreMes,
                SUM(ol.Quantity * ol.UnitPrice)                                      AS IngresosBrutos,
                SUM(ol.Quantity * ol.UnitCost)                                       AS CostoTotal,
                SUM((ol.UnitPrice - ol.UnitCost) * ol.Quantity - ol.TaxAmount)       AS MargenBruto
            FROM Sales.Orders o
            INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @FechaIni AND @FechaFin
            GROUP BY YEAR(o.OrderDate), MONTH(o.OrderDate), DATENAME(MONTH, o.OrderDate)
            ORDER BY Anio, Mes"

        Dim filas As New List(Of Object)
        Using conn As New SqlConnection(ConnStr)
            Using cmd As New SqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("@FechaIni", fechaIni)
                cmd.Parameters.AddWithValue("@FechaFin", fechaFin)
                conn.Open()
                Using dr As SqlDataReader = cmd.ExecuteReader()
                    While dr.Read()
                        filas.Add(New With {
                            .mes = $"{dr("NombreMes")} {dr("Anio")}",
                            .ingresos = Math.Round(Convert.ToDecimal(dr("IngresosBrutos")), 2),
                            .costo = Math.Round(Convert.ToDecimal(dr("CostoTotal")), 2),
                            .margen = Math.Round(Convert.ToDecimal(dr("MargenBruto")), 2)
                        })
                    End While
                End Using
            End Using
        End Using
        Return filas
    End Function

    ' ─────────────────────────────────────────────
    '  Visual 2 — Top 10 clientes por rentabilidad
    ' ─────────────────────────────────────────────
    Private Function CargarTop10Clientes(fechaIni As Date, fechaFin As Date) As Object
        Dim sql As String = "
            SELECT TOP 10
                c.CustomerName,
                SUM((ol.UnitPrice - ol.UnitCost) * ol.Quantity - ol.TaxAmount) AS MargenNeto
            FROM Sales.Customers c
            INNER JOIN Sales.Orders o  ON c.CustomerID = o.CustomerID
            INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @FechaIni AND @FechaFin
            GROUP BY c.CustomerID, c.CustomerName
            ORDER BY MargenNeto DESC"

        Dim filas As New List(Of Object)
        Using conn As New SqlConnection(ConnStr)
            Using cmd As New SqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("@FechaIni", fechaIni)
                cmd.Parameters.AddWithValue("@FechaFin", fechaFin)
                conn.Open()
                Using dr As SqlDataReader = cmd.ExecuteReader()
                    While dr.Read()
                        filas.Add(New With {
                            .cliente = dr("CustomerName").ToString(),
                            .margen = Math.Round(Convert.ToDecimal(dr("MargenNeto")), 2)
                        })
                    End While
                End Using
            End Using
        End Using
        Return filas
    End Function

    ' ─────────────────────────────────────────────
    '  Visual 3 — Margen por categoría de producto
    ' ─────────────────────────────────────────────
    Private Function CargarMargenCategoria(fechaIni As Date, fechaFin As Date) As Object
        Dim sql As String = "
            SELECT
                sg.StockGroupName AS Categoria,
                SUM((ol.UnitPrice - ol.UnitCost) * ol.Quantity - ol.TaxAmount) AS MargenBruto
            FROM Warehouse.StockGroups sg
            INNER JOIN Warehouse.StockItemStockGroups sisg ON sg.StockGroupID = sisg.StockGroupID
            INNER JOIN Sales.OrderLines ol ON sisg.StockItemID = ol.StockItemID
            INNER JOIN Sales.Orders o ON ol.OrderID = o.OrderID
            WHERE o.OrderDate BETWEEN @FechaIni AND @FechaFin
            GROUP BY sg.StockGroupID, sg.StockGroupName
            ORDER BY MargenBruto DESC"

        Dim filas As New List(Of Object)
        Using conn As New SqlConnection(ConnStr)
            Using cmd As New SqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("@FechaIni", fechaIni)
                cmd.Parameters.AddWithValue("@FechaFin", fechaFin)
                conn.Open()
                Using dr As SqlDataReader = cmd.ExecuteReader()
                    While dr.Read()
                        filas.Add(New With {
                            .categoria = dr("Categoria").ToString(),
                            .margen = Math.Round(Convert.ToDecimal(dr("MargenBruto")), 2)
                        })
                    End While
                End Using
            End Using
        End Using
        Return filas
    End Function

    ' ─────────────────────────────────────────────
    '  Visual 4 — Eficiencia del vendedor
    ' ─────────────────────────────────────────────
    Private Function CargarEficienciaVendedor(fechaIni As Date, fechaFin As Date) As Object
        Dim sql As String = "
            SELECT
                p.FullName AS Vendedor,
                SUM(ol.ExtendedPrice)                                          AS RevenuTotal,
                CASE WHEN SUM(ol.ExtendedPrice) = 0 THEN 0
                     ELSE ROUND(SUM(ol.TaxAmount) * 100.0 / SUM(ol.ExtendedPrice), 2)
                END AS PctDescuento
            FROM Application.People p
            INNER JOIN Sales.Orders o  ON p.PersonID = o.SalespersonPersonID
            INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @FechaIni AND @FechaFin
            GROUP BY p.PersonID, p.FullName
            ORDER BY RevenuTotal DESC"

        Dim filas As New List(Of Object)
        Using conn As New SqlConnection(ConnStr)
            Using cmd As New SqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("@FechaIni", fechaIni)
                cmd.Parameters.AddWithValue("@FechaFin", fechaFin)
                conn.Open()
                Using dr As SqlDataReader = cmd.ExecuteReader()
                    While dr.Read()
                        filas.Add(New With {
                            .vendedor = dr("Vendedor").ToString(),
                            .revenue = Math.Round(Convert.ToDecimal(dr("RevenuTotal")), 2),
                            .pctDescuento = Convert.ToDouble(dr("PctDescuento")),
                            .alerta = Convert.ToDouble(dr("PctDescuento")) > 10
                        })
                    End While
                End Using
            End Using
        End Using
        Return filas
    End Function

    ' ─────────────────────────────────────────────
    '  Visual 5 — KPI Strip ejecutivo
    ' ─────────────────────────────────────────────
    Private Sub CargarKPIs(fechaIni As Date, fechaFin As Date)
        ' Período anterior equivalente (misma duración corrida hacia atrás)
        Dim span As TimeSpan = fechaFin - fechaIni
        Dim periodoAntIni As Date = fechaIni.AddDays(-(span.Days + 1))
        Dim periodoAntFin As Date = fechaIni.AddDays(-1)

        Dim sql As String = "
            -- KPI 1: Ingresos del período actual
            SELECT SUM(ol.ExtendedPrice)
            FROM Sales.Orders o INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @FechaIni AND @FechaFin;

            -- KPI 2: Margen bruto % del período actual
            SELECT CASE WHEN SUM(ol.ExtendedPrice) = 0 THEN 0
                        ELSE ROUND(SUM((ol.UnitPrice - ol.UnitCost) * ol.Quantity) * 100.0 / SUM(ol.ExtendedPrice), 2)
                   END
            FROM Sales.Orders o INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @FechaIni AND @FechaFin;

            -- KPI 3: Total órdenes período actual
            SELECT COUNT(DISTINCT o.OrderID)
            FROM Sales.Orders o
            WHERE o.OrderDate BETWEEN @FechaIni AND @FechaFin;

            -- KPI 4: Ticket promedio período actual
            SELECT CASE WHEN COUNT(DISTINCT o.OrderID) = 0 THEN 0
                        ELSE ROUND(SUM(ol.ExtendedPrice) / COUNT(DISTINCT o.OrderID), 2)
                   END
            FROM Sales.Orders o INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @FechaIni AND @FechaFin;

            -- KPI 1 anterior
            SELECT ISNULL(SUM(ol.ExtendedPrice),0)
            FROM Sales.Orders o INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @PeriodoAntIni AND @PeriodoAntFin;

            -- KPI 2 anterior
            SELECT CASE WHEN SUM(ol.ExtendedPrice) = 0 THEN 0
                        ELSE ROUND(SUM((ol.UnitPrice - ol.UnitCost) * ol.Quantity) * 100.0 / SUM(ol.ExtendedPrice), 2)
                   END
            FROM Sales.Orders o INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @PeriodoAntIni AND @PeriodoAntFin;

            -- KPI 3 anterior
            SELECT COUNT(DISTINCT o.OrderID)
            FROM Sales.Orders o
            WHERE o.OrderDate BETWEEN @PeriodoAntIni AND @PeriodoAntFin;

            -- KPI 4 anterior
            SELECT CASE WHEN COUNT(DISTINCT o.OrderID) = 0 THEN 0
                        ELSE ROUND(SUM(ol.ExtendedPrice) / COUNT(DISTINCT o.OrderID), 2)
                   END
            FROM Sales.Orders o INNER JOIN Sales.OrderLines ol ON o.OrderID = ol.OrderID
            WHERE o.OrderDate BETWEEN @PeriodoAntIni AND @PeriodoAntFin;"

        Dim vals(7) As Decimal
        Using conn As New SqlConnection(ConnStr)
            Using cmd As New SqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("@FechaIni", fechaIni)
                cmd.Parameters.AddWithValue("@FechaFin", fechaFin)
                cmd.Parameters.AddWithValue("@PeriodoAntIni", periodoAntIni)
                cmd.Parameters.AddWithValue("@PeriodoAntFin", periodoAntFin)
                conn.Open()
                Using ds As SqlDataReader = cmd.ExecuteReader()
                    Dim idx As Integer = 0
                    Do
                        If ds.Read() Then
                            vals(idx) = If(ds.IsDBNull(0), 0D, Convert.ToDecimal(ds(0)))
                        End If
                        idx += 1
                    Loop While ds.NextResult() AndAlso idx < 8
                End Using
            End Using
        End Using

        ' Emitir a la página via script
        Dim kpiData As Object = New With {
            .ingresosActual = Math.Round(vals(0), 2),
            .margenPctActual = Math.Round(vals(1), 2),
            .ordenesActual = CInt(vals(2)),
            .ticketActual = Math.Round(vals(3), 2),
            .ingresosAnt = Math.Round(vals(4), 2),
            .margenPctAnt = Math.Round(vals(5), 2),
            .ordenesAnt = CInt(vals(6)),
            .ticketAnt = Math.Round(vals(7), 2)
        }
        EmitirScript("dashKPIs", kpiData)
    End Sub

    ' ─────────────────────────────────────────────
    '  Helper: serializa objeto → JS var global
    ' ─────────────────────────────────────────────
    Private Sub EmitirScript(varName As String, data As Object)
        Dim json As String = JsonConvert.SerializeObject(data)
        Dim script As String = $"var {varName} = {json};"
        ClientScript.RegisterStartupScript(Me.GetType(), varName, script, True)
    End Sub

End Class
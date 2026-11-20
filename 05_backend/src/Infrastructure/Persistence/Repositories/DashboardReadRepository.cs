using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Dapper;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Las mismas consultas de /legacy/Dashboard.aspx.vb contra WideWorldImporters, ahora con
/// Dapper y CancellationToken. Los importes se redondean a 2 decimales como Math.Round(x, 2).
/// </summary>
public sealed class DashboardReadRepository(IWwiDbConnectionFactory connectionFactory) : IDashboardReadRepository
{
    private const int DefaultCommandTimeoutSeconds = 300;
    private readonly IWwiDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<DashboardKpisDto> GetKpisAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                CAST(ISNULL(SUM(il.[ExtendedPrice]), 0) AS decimal(19, 2)) AS Revenue,
                CAST(CASE WHEN ISNULL(SUM(il.[ExtendedPrice]), 0) = 0 THEN 0
                          ELSE ROUND(SUM(il.[LineProfit]) * 100.0 / SUM(il.[ExtendedPrice]), 2)
                     END AS decimal(19, 2)) AS GrossMarginPct,
                COUNT(DISTINCT i.[InvoiceID]) AS Orders,
                CAST(CASE WHEN COUNT(DISTINCT i.[InvoiceID]) = 0 THEN 0
                          ELSE ROUND(SUM(il.[ExtendedPrice]) / COUNT(DISTINCT i.[InvoiceID]), 2)
                     END AS decimal(19, 2)) AS AverageTicket
            FROM [WideWorldImporters].[Sales].[Invoices] i
            INNER JOIN [WideWorldImporters].[Sales].[InvoiceLines] il ON i.[InvoiceID] = il.[InvoiceID]
            WHERE i.[InvoiceDate] BETWEEN @StartDate AND @EndDate;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<DashboardKpisDto>(Command(sql, startDate, endDate, cancellationToken));
    }

    public Task<IReadOnlyList<MonthlyRevenueDto>> GetMonthlyRevenueAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                YEAR(o.[OrderDate]) AS [Year],
                MONTH(o.[OrderDate]) AS [Month],
                CAST(ROUND(SUM(ol.[Quantity] * ol.[UnitPrice]), 2) AS decimal(19, 2)) AS GrossRevenue,
                CAST(ROUND(SUM(ol.[Quantity] * ol.[UnitPrice] * ol.[TaxRate] / 100.0), 2) AS decimal(19, 2)) AS Taxes,
                CAST(ROUND(SUM(ol.[Quantity] * ol.[UnitPrice] * (1 + ol.[TaxRate] / 100.0)), 2) AS decimal(19, 2)) AS TotalRevenue
            FROM [WideWorldImporters].[Sales].[Orders] o
            INNER JOIN [WideWorldImporters].[Sales].[OrderLines] ol ON o.[OrderID] = ol.[OrderID]
            WHERE o.[OrderDate] BETWEEN @StartDate AND @EndDate
            GROUP BY YEAR(o.[OrderDate]), MONTH(o.[OrderDate])
            ORDER BY [Year], [Month];
            """;

        return QueryListAsync<MonthlyRevenueDto>(sql, startDate, endDate, cancellationToken);
    }

    public Task<IReadOnlyList<CustomerProfitDto>> GetTopCustomersAsync(
        DateTime startDate, DateTime endDate, int top, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (@Top)
                c.[CustomerName] AS Customer,
                CAST(ROUND(SUM(il.[LineProfit]), 2) AS decimal(19, 2)) AS NetMargin
            FROM [WideWorldImporters].[Sales].[Customers] c
            INNER JOIN [WideWorldImporters].[Sales].[Invoices] i ON c.[CustomerID] = i.[CustomerID]
            INNER JOIN [WideWorldImporters].[Sales].[InvoiceLines] il ON i.[InvoiceID] = il.[InvoiceID]
            WHERE i.[InvoiceDate] BETWEEN @StartDate AND @EndDate
            GROUP BY c.[CustomerID], c.[CustomerName]
            ORDER BY NetMargin DESC;
            """;

        return QueryListAsync<CustomerProfitDto>(sql, startDate, endDate, cancellationToken, top);
    }

    public Task<IReadOnlyList<CategoryMarginDto>> GetCategoryMarginsAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                'Grupo ' + CAST(sisg.[StockGroupID] AS varchar(10)) AS Category,
                CAST(ROUND(SUM(il.[LineProfit]), 2) AS decimal(19, 2)) AS GrossMargin
            FROM [WideWorldImporters].[Warehouse].[StockItemStockGroups] sisg
            INNER JOIN [WideWorldImporters].[Sales].[InvoiceLines] il ON sisg.[StockItemID] = il.[StockItemID]
            INNER JOIN [WideWorldImporters].[Sales].[Invoices] i ON il.[InvoiceID] = i.[InvoiceID]
            WHERE i.[InvoiceDate] BETWEEN @StartDate AND @EndDate
            GROUP BY sisg.[StockGroupID]
            ORDER BY GrossMargin DESC;
            """;

        return QueryListAsync<CategoryMarginDto>(sql, startDate, endDate, cancellationToken);
    }

    public Task<IReadOnlyList<SalespersonSalesDto>> GetSalespeopleAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                p.[FullName] AS Salesperson,
                CAST(ROUND(SUM(il.[ExtendedPrice]), 2) AS decimal(19, 2)) AS Revenue,
                CAST(CASE WHEN SUM(il.[ExtendedPrice]) = 0 THEN 0
                          ELSE ROUND(SUM(il.[LineProfit]) * 100.0 / SUM(il.[ExtendedPrice]), 2)
                     END AS decimal(19, 2)) AS MarginPct
            FROM [WideWorldImporters].[Application].[People] p
            INNER JOIN [WideWorldImporters].[Sales].[Invoices] i ON p.[PersonID] = i.[SalespersonPersonID]
            INNER JOIN [WideWorldImporters].[Sales].[InvoiceLines] il ON i.[InvoiceID] = il.[InvoiceID]
            WHERE i.[InvoiceDate] BETWEEN @StartDate AND @EndDate
            GROUP BY p.[PersonID], p.[FullName]
            ORDER BY Revenue DESC;
            """;

        return QueryListAsync<SalespersonSalesDto>(sql, startDate, endDate, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> QueryListAsync<T>(
        string sql,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken,
        int? top = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<T>(Command(sql, startDate, endDate, cancellationToken, top));
        return rows.AsList();
    }

    private static CommandDefinition Command(
        string sql,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken,
        int? top = null) =>
        new(
            commandText: sql,
            parameters: new { StartDate = startDate, EndDate = endDate, Top = top ?? 0 },
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken);
}

using Application.Common.Models;
using Application.Features.Sales.Abstractions;
using Application.Features.Sales.Dtos;
using Dapper;

namespace Infrastructure.Persistence.Repositories;

public sealed class SalesReadRepository(IWwiDbConnectionFactory connectionFactory) : ISalesReadRepository
{
    private const int DefaultCommandTimeoutSeconds = 300;
    private readonly IWwiDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<PagedResult<CustomerTransactionDto>> GetCustomerTransactionsAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM [WideWorldImporters].[Sales].[CustomerTransactions]
            WHERE [TransactionDate] >= @StartDate AND [TransactionDate] <= @EndDate;

            SELECT
                [CustomerTransactionID],
                [CustomerID],
                [TransactionTypeID],
                [InvoiceID],
                [PaymentMethodID],
                [TransactionDate],
                [AmountExcludingTax],
                [TaxAmount],
                [TransactionAmount],
                [OutstandingBalance],
                [FinalizationDate],
                [IsFinalized],
                [LastEditedBy],
                [LastEditedWhen]
            FROM [WideWorldImporters].[Sales].[CustomerTransactions]
            WHERE [TransactionDate] >= @StartDate AND [TransactionDate] <= @EndDate
            ORDER BY [TransactionDate] DESC, [CustomerTransactionID] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        return QueryPagedAsync<CustomerTransactionDto>(sql, startDate, endDate, page, pageSize, cancellationToken);
    }

    public Task<PagedResult<InvoiceLineDto>> GetInvoiceLinesAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM [WideWorldImporters].[Sales].[InvoiceLines]
            WHERE [LastEditedWhen] >= @StartDate AND [LastEditedWhen] <= @EndDate;

            SELECT
                [InvoiceLineID],
                [InvoiceID],
                [StockItemID],
                [Description],
                [PackageTypeID],
                [Quantity],
                [UnitPrice],
                [TaxRate],
                [TaxAmount],
                [LineProfit],
                [ExtendedPrice],
                [LastEditedBy],
                [LastEditedWhen]
            FROM [WideWorldImporters].[Sales].[InvoiceLines]
            WHERE [LastEditedWhen] >= @StartDate AND [LastEditedWhen] <= @EndDate
            ORDER BY [LastEditedWhen] DESC, [InvoiceLineID] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        return QueryPagedAsync<InvoiceLineDto>(sql, startDate, endDate, page, pageSize, cancellationToken);
    }

    public Task<PagedResult<InvoiceDto>> GetInvoicesAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM [WideWorldImporters].[Sales].[Invoices]
            WHERE [InvoiceDate] >= @StartDate AND [InvoiceDate] <= @EndDate;

            SELECT
                [InvoiceID],
                [CustomerID],
                [BillToCustomerID],
                [OrderID],
                [DeliveryMethodID],
                [ContactPersonID],
                [AccountsPersonID],
                [SalespersonPersonID],
                [PackedByPersonID],
                [InvoiceDate],
                [CustomerPurchaseOrderNumber],
                [IsCreditNote],
                [CreditNoteReason],
                [Comments],
                [DeliveryInstructions],
                [InternalComments],
                [TotalDryItems],
                [TotalChillerItems],
                [DeliveryRun],
                [RunPosition],
                [ReturnedDeliveryData],
                [ConfirmedDeliveryTime],
                [ConfirmedReceivedBy],
                [LastEditedBy],
                [LastEditedWhen]
            FROM [WideWorldImporters].[Sales].[Invoices]
            WHERE [InvoiceDate] >= @StartDate AND [InvoiceDate] <= @EndDate
            ORDER BY [InvoiceDate] DESC, [InvoiceID] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        return QueryPagedAsync<InvoiceDto>(sql, startDate, endDate, page, pageSize, cancellationToken);
    }

    public Task<PagedResult<OrderLineDto>> GetOrderLinesAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM [WideWorldImporters].[Sales].[OrderLines]
            WHERE [LastEditedWhen] >= @StartDate AND [LastEditedWhen] <= @EndDate;

            SELECT
                [OrderLineID],
                [OrderID],
                [StockItemID],
                [Description],
                [PackageTypeID],
                [Quantity],
                [UnitPrice],
                [TaxRate],
                [PickedQuantity],
                [PickingCompletedWhen],
                [LastEditedBy],
                [LastEditedWhen]
            FROM [WideWorldImporters].[Sales].[OrderLines]
            WHERE [LastEditedWhen] >= @StartDate AND [LastEditedWhen] <= @EndDate
            ORDER BY [LastEditedWhen] DESC, [OrderLineID] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        return QueryPagedAsync<OrderLineDto>(sql, startDate, endDate, page, pageSize, cancellationToken);
    }

    public Task<PagedResult<OrderDto>> GetOrdersAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM [WideWorldImporters].[Sales].[Orders]
            WHERE [OrderDate] >= @StartDate AND [OrderDate] <= @EndDate;

            SELECT
                [OrderID],
                [CustomerID],
                [SalespersonPersonID],
                [PickedByPersonID],
                [ContactPersonID],
                [BackorderOrderID],
                [OrderDate],
                [ExpectedDeliveryDate],
                [CustomerPurchaseOrderNumber],
                [IsUndersupplyBackordered],
                [Comments],
                [DeliveryInstructions],
                [InternalComments],
                [PickingCompletedWhen],
                [LastEditedBy],
                [LastEditedWhen]
            FROM [WideWorldImporters].[Sales].[Orders]
            WHERE [OrderDate] >= @StartDate AND [OrderDate] <= @EndDate
            ORDER BY [OrderDate] DESC, [OrderID] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        return QueryPagedAsync<OrderDto>(sql, startDate, endDate, page, pageSize, cancellationToken);
    }

    private async Task<PagedResult<T>> QueryPagedAsync<T>(
        string sql,
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new
        {
            StartDate = startDate,
            EndDate = endDate,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        };

        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(command);
        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<T>()).AsList();

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }
}

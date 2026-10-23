using Application.Common.Models;
using Application.Features.Sales.Dtos;

namespace Application.Features.Sales.Abstractions;

public interface ISalesReadRepository
{
    Task<PagedResult<CustomerTransactionDto>> GetCustomerTransactionsAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InvoiceLineDto>> GetInvoiceLinesAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InvoiceDto>> GetInvoicesAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OrderLineDto>> GetOrderLinesAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OrderDto>> GetOrdersAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

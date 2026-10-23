using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Sales.Dtos;

namespace Application.Features.Sales.Queries;

public sealed record GetCustomerTransactionsQuery(
    DateTime StartDate,
    DateTime EndDate,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<CustomerTransactionDto>>;

public sealed record GetInvoiceLinesQuery(
    DateTime StartDate,
    DateTime EndDate,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<InvoiceLineDto>>;

public sealed record GetInvoicesQuery(
    DateTime StartDate,
    DateTime EndDate,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<InvoiceDto>>;

public sealed record GetOrderLinesQuery(
    DateTime StartDate,
    DateTime EndDate,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<OrderLineDto>>;

public sealed record GetOrdersQuery(
    DateTime StartDate,
    DateTime EndDate,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<OrderDto>>;

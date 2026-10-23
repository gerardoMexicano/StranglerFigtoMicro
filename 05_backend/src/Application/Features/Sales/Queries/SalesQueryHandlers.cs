using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Sales.Abstractions;
using Application.Features.Sales.Dtos;
using Domain.Common;

namespace Application.Features.Sales.Queries;

internal static class SalesQueryValidation
{
    public const int MaxPageSize = 500;

    public static Error? Validate(DateTime startDate, DateTime endDate, int page, int pageSize)
    {
        if (startDate.Date > endDate.Date)
        {
            return Error.Validation(
                "Sales.InvalidDateRange",
                "La fecha de inicio no puede ser mayor que la fecha final.");
        }

        if (page < 1)
        {
            return Error.Validation(
                "Pagination.InvalidPage",
                "El número de página debe ser mayor o igual a 1.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return Error.Validation(
                "Pagination.InvalidPageSize",
                $"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
        }

        return null;
    }

    public static (DateTime NormalizedStart, DateTime NormalizedEnd) NormalizeDates(DateTime startDate, DateTime endDate) =>
        (startDate.Date, endDate.Date.AddDays(1).AddTicks(-1));
}

public sealed class GetCustomerTransactionsQueryHandler(ISalesReadRepository repository)
    : IQueryHandler<GetCustomerTransactionsQuery, PagedResult<CustomerTransactionDto>>
{
    private readonly ISalesReadRepository _repository = repository;

    public async Task<Result<PagedResult<CustomerTransactionDto>>> HandleAsync(
        GetCustomerTransactionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = SalesQueryValidation.Validate(query.StartDate, query.EndDate, query.Page, query.PageSize);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<CustomerTransactionDto>>(validationError);
        }

        var (start, end) = SalesQueryValidation.NormalizeDates(query.StartDate, query.EndDate);
        var pageResult = await _repository.GetCustomerTransactionsAsync(
            start, end, query.Page, query.PageSize, cancellationToken);

        return Result.Success(pageResult);
    }
}

public sealed class GetInvoiceLinesQueryHandler(ISalesReadRepository repository)
    : IQueryHandler<GetInvoiceLinesQuery, PagedResult<InvoiceLineDto>>
{
    private readonly ISalesReadRepository _repository = repository;

    public async Task<Result<PagedResult<InvoiceLineDto>>> HandleAsync(
        GetInvoiceLinesQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = SalesQueryValidation.Validate(query.StartDate, query.EndDate, query.Page, query.PageSize);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<InvoiceLineDto>>(validationError);
        }

        var (start, end) = SalesQueryValidation.NormalizeDates(query.StartDate, query.EndDate);
        var pageResult = await _repository.GetInvoiceLinesAsync(
            start, end, query.Page, query.PageSize, cancellationToken);

        return Result.Success(pageResult);
    }
}

public sealed class GetInvoicesQueryHandler(ISalesReadRepository repository)
    : IQueryHandler<GetInvoicesQuery, PagedResult<InvoiceDto>>
{
    private readonly ISalesReadRepository _repository = repository;

    public async Task<Result<PagedResult<InvoiceDto>>> HandleAsync(
        GetInvoicesQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = SalesQueryValidation.Validate(query.StartDate, query.EndDate, query.Page, query.PageSize);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<InvoiceDto>>(validationError);
        }

        var (start, end) = SalesQueryValidation.NormalizeDates(query.StartDate, query.EndDate);
        var pageResult = await _repository.GetInvoicesAsync(
            start, end, query.Page, query.PageSize, cancellationToken);

        return Result.Success(pageResult);
    }
}

public sealed class GetOrderLinesQueryHandler(ISalesReadRepository repository)
    : IQueryHandler<GetOrderLinesQuery, PagedResult<OrderLineDto>>
{
    private readonly ISalesReadRepository _repository = repository;

    public async Task<Result<PagedResult<OrderLineDto>>> HandleAsync(
        GetOrderLinesQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = SalesQueryValidation.Validate(query.StartDate, query.EndDate, query.Page, query.PageSize);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<OrderLineDto>>(validationError);
        }

        var (start, end) = SalesQueryValidation.NormalizeDates(query.StartDate, query.EndDate);
        var pageResult = await _repository.GetOrderLinesAsync(
            start, end, query.Page, query.PageSize, cancellationToken);

        return Result.Success(pageResult);
    }
}

public sealed class GetOrdersQueryHandler(ISalesReadRepository repository)
    : IQueryHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    private readonly ISalesReadRepository _repository = repository;

    public async Task<Result<PagedResult<OrderDto>>> HandleAsync(
        GetOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = SalesQueryValidation.Validate(query.StartDate, query.EndDate, query.Page, query.PageSize);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<OrderDto>>(validationError);
        }

        var (start, end) = SalesQueryValidation.NormalizeDates(query.StartDate, query.EndDate);
        var pageResult = await _repository.GetOrdersAsync(
            start, end, query.Page, query.PageSize, cancellationToken);

        return Result.Success(pageResult);
    }
}

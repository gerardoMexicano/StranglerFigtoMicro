using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Taxes.Abstractions;
using Application.Features.Taxes.Dtos;
using Domain.Common;

namespace Application.Features.Taxes.Queries;

internal static class TaxesQueryValidation
{
    public const int MaxPageSize = 500;

    public static Error? Validate(DateTime startDate, DateTime endDate, int page, int pageSize)
    {
        if (startDate.Date > endDate.Date)
        {
            return Error.Validation(
                "Taxes.InvalidDateRange",
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

public sealed class GetConsolidatedReportQueryHandler(ITaxesReadRepository repository)
    : IQueryHandler<GetConsolidatedReportQuery, PagedResult<ConsolidatedReportItemDto>>
{
    private readonly ITaxesReadRepository _repository = repository;

    public async Task<Result<PagedResult<ConsolidatedReportItemDto>>> HandleAsync(
        GetConsolidatedReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = TaxesQueryValidation.Validate(query.StartDate, query.EndDate, query.Page, query.PageSize);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<ConsolidatedReportItemDto>>(validationError);
        }

        var (start, end) = TaxesQueryValidation.NormalizeDates(query.StartDate, query.EndDate);
        var pageResult = await _repository.GetConsolidatedReportAsync(
            start, end, query.Page, query.PageSize, cancellationToken);

        return Result.Success(pageResult);
    }
}

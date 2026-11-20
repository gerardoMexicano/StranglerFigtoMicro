using Application.Common.Messaging;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Domain.Common;

namespace Application.Features.Dashboard.Queries;

public sealed record GetExecutiveDashboardQuery(
    DateTime StartDate,
    DateTime EndDate) : IQuery<ExecutiveDashboardDto>;

/// <summary>
/// Caso de uso extraído de /legacy/Dashboard.aspx: los cinco visuales del Dashboard Ejecutivo
/// de WideWorldImporters en una sola respuesta, como el postback original.
/// </summary>
public sealed class GetExecutiveDashboardQueryHandler(IDashboardReadRepository repository)
    : IQueryHandler<GetExecutiveDashboardQuery, ExecutiveDashboardDto>
{
    public const int TopCustomers = 10;
    public const decimal LowMarginThresholdPct = 20m;

    private readonly IDashboardReadRepository _repository = repository;

    public async Task<Result<ExecutiveDashboardDto>> HandleAsync(
        GetExecutiveDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        // InvoiceDate y OrderDate son DATE: la hora del filtro no cuenta (BETWEEN inclusivo)
        var start = query.StartDate.Date;
        var end = query.EndDate.Date;

        if (start > end)
        {
            return Result.Failure<ExecutiveDashboardDto>(Error.Validation(
                "Dashboard.InvalidDateRange",
                "La fecha de inicio no puede ser mayor que la fecha final."));
        }

        // Período anterior equivalente (misma duración corrida hacia atrás), como CargarKPIs
        var days = (end - start).Days;
        var previousStart = start.AddDays(-(days + 1));
        var previousEnd = start.AddDays(-1);

        var current = await _repository.GetKpisAsync(start, end, cancellationToken);
        var previous = await _repository.GetKpisAsync(previousStart, previousEnd, cancellationToken);
        var months = await _repository.GetMonthlyRevenueAsync(start, end, cancellationToken);
        var customers = await _repository.GetTopCustomersAsync(start, end, TopCustomers, cancellationToken);
        var categories = await _repository.GetCategoryMarginsAsync(start, end, cancellationToken);
        var salespeople = await _repository.GetSalespeopleAsync(start, end, cancellationToken);

        return Result.Success(new ExecutiveDashboardDto(
            start,
            end,
            new ExecutiveKpisDto(current, previous, previousStart, previousEnd),
            months,
            customers,
            categories,
            salespeople
                .Select(s => new SalespersonEfficiencyDto(
                    s.Salesperson,
                    s.Revenue,
                    s.MarginPct,
                    LowMargin: s.MarginPct < LowMarginThresholdPct))
                .ToList()));
    }
}

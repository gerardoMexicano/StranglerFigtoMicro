using Application.Common.Messaging;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Domain.Common;

namespace Application.Features.Dashboard.Queries;

public sealed record GetExecutiveDashboardQuery(
    DateTime StartDate,
    DateTime EndDate) : IQuery<ExecutiveDashboardDto>;

public sealed class GetExecutiveDashboardQueryHandler(IDashboardReadRepository repository)
    : IQueryHandler<GetExecutiveDashboardQuery, ExecutiveDashboardDto>
{
    public const int TopCustomers = 10;
    public const decimal LowMarginThresholdPct = 20m;

    private readonly IDashboardReadRepository _repository = repository;

    public Task<Result<ExecutiveDashboardDto>> HandleAsync(
        GetExecutiveDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        // Fase Red (TDD): contrato mínimo, la implementación llega tras aprobar las pruebas
        throw new NotImplementedException();
    }
}

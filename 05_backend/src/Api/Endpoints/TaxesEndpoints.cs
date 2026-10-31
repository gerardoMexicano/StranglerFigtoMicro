using Api.Extensions;
using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Taxes.Dtos;
using Application.Features.Taxes.Queries;

namespace Api.Endpoints;

public sealed class TaxesEndpoints : IEndpoint
{
    private static readonly DateTime DefaultStartDate = new(2020, 1, 1);
    private static readonly DateTime DefaultEndDate = new(2026, 12, 31);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/taxes")
            .WithTags("Taxes (PaginaAntiguaNuevoBackend)")
            .RequireAuthorization("Lectura");

        group.MapGet("/consolidated-report", async (
            DateTime? startDate,
            DateTime? endDate,
            int? page,
            int? pageSize,
            IQueryHandler<GetConsolidatedReportQuery, PagedResult<ConsolidatedReportItemDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetConsolidatedReportQuery(
                StartDate: startDate ?? DefaultStartDate,
                EndDate: endDate ?? DefaultEndDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .WithName("GetTaxesConsolidatedReport")
        .WithSummary("Consulta paginada del Reporte Masivo Consolidado (CfdiCore + Complementos + Nómina) en taxes_proto por rango de fechas.")
        .Produces<PagedResult<ConsolidatedReportItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/seismic-geojson", async (
            int? count,
            double? minMagnitude,
            int? seed,
            IQueryHandler<GetSeismicComparisonQuery, SeismicComparisonResultDto> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetSeismicComparisonQuery(
                Count: count ?? 5000,
                MinMagnitude: minMagnitude ?? 1.0,
                Seed: seed ?? 2026);

            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .WithName("GetSeismicGeoJson")
        .WithSummary("Generación y procesamiento determinista de puntos sísmicos GeoJSON para la comparativa de rendimiento en /taxes/Home/PaginaAngular.")
        .Produces<SeismicComparisonResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

using Api.Extensions;
using Application.Common.Messaging;
using Application.Features.Dashboard.Dtos;
using Application.Features.Dashboard.Queries;

namespace Api.Endpoints;

public sealed class DashboardEndpoints : IEndpoint
{
    // WideWorldImporters llega hasta 2016-05-31: el default de Dashboard.aspx (año en curso) saldría vacío
    private static readonly DateTime DefaultStartDate = new(2016, 1, 1);
    private static readonly DateTime DefaultEndDate = new(2016, 5, 31);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard (System Principal)")
            .RequireAuthorization("Lectura");

        group.MapGet("/executive", async (
            DateTime? startDate,
            DateTime? endDate,
            IQueryHandler<GetExecutiveDashboardQuery, ExecutiveDashboardDto> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetExecutiveDashboardQuery(
                StartDate: startDate ?? DefaultStartDate,
                EndDate: endDate ?? DefaultEndDate);

            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .WithName("GetExecutiveDashboard")
        .WithSummary("Dashboard Ejecutivo de WideWorldImporters (KPIs vs período anterior, ingresos mensuales, top 10 clientes, margen por categoría y eficiencia del vendedor).")
        .Produces<ExecutiveDashboardDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

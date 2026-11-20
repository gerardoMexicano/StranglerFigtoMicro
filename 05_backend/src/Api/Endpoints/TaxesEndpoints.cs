using System.Text;
using Api.Extensions;
using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Taxes.Commands;
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
            .WithTags("Taxes (PaginaAntigua y PaginaAntiguaNuevoBackend)");

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
        .RequireAuthorization("Lectura")
        .WithName("GetTaxesConsolidatedReport")
        .WithSummary("Consulta paginada del Reporte Masivo Consolidado (CfdiCore + Complementos + Nómina) en taxes_proto por rango de fechas.")
        .Produces<PagedResult<ConsolidatedReportItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/payroll-summary", async (
            DateTime? startDate,
            DateTime? endDate,
            IQueryHandler<GetPayrollSummaryQuery, PayrollSummaryReportDto> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetPayrollSummaryQuery(startDate, endDate);
            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .RequireAuthorization("Lectura")
        .WithName("GetTaxesPayrollSummary")
        .WithSummary("Dashboard de Nómina (PaginaAntigua/Index): KPIs, costo por departamento y listado de recibos.")
        .Produces<PayrollSummaryReportDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/xml-batch", async (
            HttpRequest request,
            ICommandHandler<UploadBulkXmlCommand, BulkXmlUploadResultDto> handler,
            CancellationToken cancellationToken) =>
        {
            var files = await ReadUploadedFilesAsync(request, cancellationToken);
            var result = await handler.HandleAsync(new UploadBulkXmlCommand(files), cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .RequireAuthorization("UsuarioOrAdmin")
        .DisableAntiforgery()
        .WithName("UploadTaxesXmlBatch")
        .WithSummary("Carga masiva de hasta 5000 archivos XML CFDI 4.0 de nómina a CfdiCore, CfdiComplemento, NominaResumen y NominaMovimiento.")
        .Produces<BulkXmlUploadResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

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
        .RequireAuthorization("Lectura")
        .WithName("GetSeismicGeoJson")
        .WithSummary("Generación y procesamiento determinista de puntos sísmicos GeoJSON para la comparativa de rendimiento en /taxes/Home/PaginaAngular.")
        .Produces<SeismicComparisonResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    internal static async Task<IReadOnlyList<XmlFilePayload>> ReadUploadedFilesAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return [];
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files.Count == 0)
        {
            return [];
        }

        var list = new List<XmlFilePayload>(form.Files.Count);
        foreach (var file in form.Files)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync(cancellationToken);
            list.Add(new XmlFilePayload(file.FileName, content));
        }

        return list;
    }
}

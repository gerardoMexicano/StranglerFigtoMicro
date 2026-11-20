using Api.Extensions;
using Application.Common.Messaging;
using Application.Features.Reports;

namespace Api.Endpoints;

public sealed class ReportsEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports")
            .WithTags("Reports (Reportes.aspx)");

        group.MapPost("/cfdi-pdf-batch", async (
            HttpRequest request,
            HttpResponse response,
            ICommandHandler<GenerateCfdiPdfBatchCommand, CfdiPdfBatchResultDto> handler,
            CancellationToken cancellationToken) =>
        {
            var files = await TaxesEndpoints.ReadUploadedFilesAsync(request, cancellationToken);
            var result = await handler.HandleAsync(new GenerateCfdiPdfBatchCommand(files), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToProblemDetails();
            }

            response.Headers["X-Received-Count"] = result.Value.TotalFilesReceived.ToString();
            response.Headers["X-Valid-Count"] = result.Value.ValidCfdiCount.ToString();
            response.Headers["X-Invalid-Count"] = result.Value.InvalidXmlCount.ToString();

            return TypedResults.File(
                fileContents: result.Value.ZipBytes,
                contentType: "application/zip",
                fileDownloadName: "ReportesGenerados.zip");
        })
        .RequireAuthorization("Lectura")
        .DisableAntiforgery()
        .WithBulkXmlUploadLimits()
        .WithName("GenerateCfdiPdfBatch")
        .WithSummary("Lee uno o varios archivos XML CFDI 4.0, genera un PDF por cada factura válida (Reporte_<UUID>.pdf) y devuelve el archivo ZIP.")
        .Produces(StatusCodes.Status200OK, contentType: "application/zip")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

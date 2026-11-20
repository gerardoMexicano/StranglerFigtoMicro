using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

/// <summary>
/// Límites de los endpoints de carga masiva de XML (/api/taxes/xml-batch y /api/reports/cfdi-pdf-batch).
/// Los valores por defecto de ASP.NET Core (30 MB por petición y 1024 entradas de formulario,
/// que también cuentan archivos) cortan un lote antes de llegar al máximo de 5000 archivos.
/// Se aplican solo a estos endpoints; el resto del backend conserva los límites por defecto.
/// Si se cambian, revisar también client_max_body_size en nginx (sitio.inc.template) y
/// MaxRequestBodySize del gateway (Gateway/Program.cs): los tres deben dejar pasar el mismo tamaño.
/// </summary>
internal static class BulkUploadLimits
{
    /// <summary>200 MB: 5000 XML CFDI de nómina pesan de 10 a 30 KB cada uno.</summary>
    public const long MaxRequestBodyBytes = 200L * 1024 * 1024;

    /// <summary>Holgura sobre los 5000 archivos permitidos por los handlers.</summary>
    public const int MaxFormEntries = 6000;

    public static RouteHandlerBuilder WithBulkXmlUploadLimits(this RouteHandlerBuilder builder) =>
        builder
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodyBytes))
            .WithFormOptions(
                multipartBodyLengthLimit: MaxRequestBodyBytes,
                valueCountLimit: MaxFormEntries);
}

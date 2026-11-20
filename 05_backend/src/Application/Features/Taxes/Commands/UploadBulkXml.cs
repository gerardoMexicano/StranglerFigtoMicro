using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Application.Common.Messaging;
using Application.Features.Taxes.Abstractions;
using Application.Features.Taxes.Dtos;
using Domain.Common;

namespace Application.Features.Taxes.Commands;

public sealed record UploadBulkXmlCommand(
    IReadOnlyList<XmlFilePayload>? Files) : ICommand<BulkXmlUploadResultDto>;

public sealed class UploadBulkXmlCommandHandler(ITaxesWriteRepository repository)
    : ICommandHandler<UploadBulkXmlCommand, BulkXmlUploadResultDto>
{
    public const int MaxFilesAllowed = 5000;

    private static readonly XNamespace NsCfdi4 = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace NsTfd = "http://www.sat.gob.mx/TimbreFiscalDigital";
    private static readonly XNamespace NsNomina12 = "http://www.sat.gob.mx/nomina12";

    private readonly ITaxesWriteRepository _repository = repository;

    public async Task<Result<BulkXmlUploadResultDto>> HandleAsync(
        UploadBulkXmlCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Files is null || command.Files.Count == 0)
        {
            return Result.Failure<BulkXmlUploadResultDto>(Error.Validation(
                "Taxes.NoFilesProvided",
                "No se recibieron archivos XML para procesar."));
        }

        if (command.Files.Count > MaxFilesAllowed)
        {
            return Result.Failure<BulkXmlUploadResultDto>(Error.Validation(
                "Taxes.TooManyFiles",
                $"Se permite cargar un máximo de {MaxFilesAllowed} archivos XML por lote."));
        }

        var sw = Stopwatch.StartNew();
        var validRecords = new List<ParsedCfdiCoreRecord>(command.Files.Count);
        var invalidCount = 0;

        foreach (var file in command.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parsed = TryParseCfdiXml(file.Content);
            if (parsed is not null)
            {
                validRecords.Add(parsed);
            }
            else
            {
                invalidCount++;
            }
        }

        if (validRecords.Count == 0)
        {
            return Result.Failure<BulkXmlUploadResultDto>(Error.Validation(
                "Taxes.NoValidXmlFiles",
                "Ninguno de los archivos enviados es un comprobante CFDI 4.0 válido."));
        }

        var persistedCount = await _repository.UpsertBatchAsync(validRecords, cancellationToken);
        sw.Stop();

        var message = $"Se procesaron {persistedCount} XMLs válidos de {command.Files.Count} recibidos en {sw.ElapsedMilliseconds} ms.";
        return Result.Success(new BulkXmlUploadResultDto(
            ReceivedCount: command.Files.Count,
            ProcessedCount: persistedCount,
            InvalidCount: invalidCount,
            ElapsedMs: sw.ElapsedMilliseconds,
            Message: message));
    }

    internal static ParsedCfdiCoreRecord? TryParseCfdiXml(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            // Sanitizar string igual que en el controlador legado (quitar Ü/ü y caracteres no válidos XML)
            var sanitized = rawContent.Replace("Ü", "U").Replace("ü", "u");
            sanitized = new string(sanitized.Where(XmlConvert.IsXmlChar).ToArray());

            var startIndex = sanitized.IndexOf("<cfdi:Comprobante", StringComparison.Ordinal);
            if (startIndex > 0)
            {
                sanitized = sanitized[startIndex..];
            }

            var doc = XDocument.Parse(sanitized, LoadOptions.PreserveWhitespace);
            var comprobante = doc.Root;
            if (comprobante is null || comprobante.Name != NsCfdi4 + "Comprobante")
            {
                comprobante = doc.Descendants(NsCfdi4 + "Comprobante").FirstOrDefault();
            }

            if (comprobante is null)
            {
                return null;
            }

            var emisor = comprobante.Element(NsCfdi4 + "Emisor");
            var receptor = comprobante.Element(NsCfdi4 + "Receptor");
            var complemento = comprobante.Element(NsCfdi4 + "Complemento");
            var tfd = complemento?.Element(NsTfd + "TimbreFiscalDigital")
                ?? doc.Descendants(NsTfd + "TimbreFiscalDigital").FirstOrDefault();

            if (emisor is null || receptor is null || tfd is null)
            {
                return null;
            }

            var uuidAttr = Attr(tfd, "UUID");
            var uuid = Guid.TryParse(uuidAttr, out var parsedGuid) ? parsedGuid : Guid.NewGuid();

            var fecha = ParseDateTime(Attr(comprobante, "Fecha")) ?? DateTime.Now;
            var fechaTimbrado = ParseDateTime(Attr(tfd, "FechaTimbrado"));

            ParsedCfdiComplemento? parsedComplemento = null;
            if (complemento is not null)
            {
                var nominaElem = complemento.Element(NsNomina12 + "Nomina");
                var tipoComplemento = nominaElem is not null ? "Nomina12" : "TFD/Otro";
                var jsonComp = SerializeElementAttributesToJson(complemento);
                parsedComplemento = new ParsedCfdiComplemento(
                    TipoComplemento: tipoComplemento,
                    JsonData: jsonComp,
                    XmlFragment: sanitized);
            }

            ParsedNominaResumen? parsedNomina = null;
            var nominaNode = complemento?.Element(NsNomina12 + "Nomina")
                ?? doc.Descendants(NsNomina12 + "Nomina").FirstOrDefault();

            if (nominaNode is not null)
            {
                var receptorNomina = nominaNode.Element(NsNomina12 + "Receptor");
                var percepcionesNode = nominaNode.Element(NsNomina12 + "Percepciones");
                var deduccionesNode = nominaNode.Element(NsNomina12 + "Deducciones");

                var totalGravado = ParseDecimal(Attr(percepcionesNode, "TotalGravado"));
                var totalExento = ParseDecimal(Attr(percepcionesNode, "TotalExento"));

                var movimientos = new List<ParsedNominaMovimiento>();

                if (percepcionesNode is not null)
                {
                    foreach (var perc in percepcionesNode.Elements(NsNomina12 + "Percepcion"))
                    {
                        var gravado = ParseDecimal(Attr(perc, "ImporteGravado"));
                        var exento = ParseDecimal(Attr(perc, "ImporteExento"));
                        movimientos.Add(new ParsedNominaMovimiento(
                            TipoMovimiento: "Percepcion",
                            TipoCodigo: Attr(perc, "TipoPercepcion"),
                            Clave: Attr(perc, "Clave"),
                            Concepto: Attr(perc, "Concepto"),
                            ImporteGravado: gravado,
                            ImporteExento: exento,
                            Importe: gravado + exento));
                    }
                }

                if (deduccionesNode is not null)
                {
                    foreach (var deduc in deduccionesNode.Elements(NsNomina12 + "Deduccion"))
                    {
                        var importe = ParseDecimal(Attr(deduc, "Importe"));
                        movimientos.Add(new ParsedNominaMovimiento(
                            TipoMovimiento: "Deduccion",
                            TipoCodigo: Attr(deduc, "TipoDeduccion"),
                            Clave: Attr(deduc, "Clave"),
                            Concepto: Attr(deduc, "Concepto"),
                            ImporteGravado: 0m,
                            ImporteExento: 0m,
                            Importe: importe));
                    }
                }

                parsedNomina = new ParsedNominaResumen(
                    TipoNomina: Attr(nominaNode, "TipoNomina"),
                    FechaPago: ParseDateTime(Attr(nominaNode, "FechaPago")),
                    FechaInicialPago: ParseDateTime(Attr(nominaNode, "FechaInicialPago")),
                    FechaFinalPago: ParseDateTime(Attr(nominaNode, "FechaFinalPago")),
                    NumDiasPagados: ParseDecimal(Attr(nominaNode, "NumDiasPagados")),
                    TotalPercepciones: ParseDecimal(Attr(nominaNode, "TotalPercepciones")),
                    TotalDeducciones: ParseDecimal(Attr(nominaNode, "TotalDeducciones")),
                    TotalGravado: totalGravado,
                    TotalExento: totalExento,
                    NumEmpleado: Attr(receptorNomina, "NumEmpleado") ?? string.Empty,
                    Departamento: Attr(receptorNomina, "Departamento"),
                    Puesto: Attr(receptorNomina, "Puesto"),
                    TipoContrato: Attr(receptorNomina, "TipoContrato"),
                    TipoRegimen: Attr(receptorNomina, "TipoRegimen"),
                    PeriodicidadPago: Attr(receptorNomina, "PeriodicidadPago"),
                    SalarioBaseCotApor: ParseDecimal(Attr(receptorNomina, "SalarioBaseCotApor")),
                    SalarioDiarioIntegrado: ParseDecimal(Attr(receptorNomina, "SalarioDiarioIntegrado")),
                    Movimientos: movimientos);
            }

            return new ParsedCfdiCoreRecord(
                UUID: uuid,
                TipoComprobante: Attr(comprobante, "TipoDeComprobante") ?? "I",
                Serie: Attr(comprobante, "Serie"),
                Folio: Attr(comprobante, "Folio"),
                Fecha: fecha,
                Moneda: Attr(comprobante, "Moneda"),
                SubTotal: ParseDecimal(Attr(comprobante, "SubTotal")),
                Descuento: ParseDecimal(Attr(comprobante, "Descuento")),
                Total: ParseDecimal(Attr(comprobante, "Total")),
                MetodoPago: Attr(comprobante, "MetodoPago"),
                LugarExpedicion: Attr(comprobante, "LugarExpedicion"),
                EmisorRFC: Attr(emisor, "Rfc") ?? string.Empty,
                EmisorNombre: Attr(emisor, "Nombre"),
                ReceptorRFC: Attr(receptor, "Rfc") ?? string.Empty,
                ReceptorNombre: Attr(receptor, "Nombre"),
                UsoCFDI: Attr(receptor, "UsoCFDI"),
                FechaTimbrado: fechaTimbrado,
                RfcProvCertif: Attr(tfd, "RfcProvCertif"),
                XmlOriginal: sanitized,
                Complemento: parsedComplemento,
                Nomina: parsedNomina);
        }
        catch
        {
            return null;
        }
    }

    private static string? Attr(XElement? element, string attributeName) =>
        element?.Attribute(attributeName)?.Value;

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    private static DateTime? ParseDateTime(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) && parsed != DateTime.MinValue
            ? parsed
            : null;

    private static string SerializeElementAttributesToJson(XElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var child in element.Elements())
        {
            var attrs = child.Attributes()
                .Where(a => !a.IsNamespaceDeclaration)
                .ToDictionary(a => a.Name.LocalName, a => a.Value);
            dict[child.Name.LocalName] = attrs;
        }
        return JsonSerializer.Serialize(dict);
    }
}

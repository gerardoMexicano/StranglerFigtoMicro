using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Application.Common.Messaging;
using Application.Features.Taxes.Dtos;
using Domain.Common;

namespace Application.Features.Reports;

public sealed record CfdiPdfBatchResultDto(
    byte[] ZipBytes,
    int TotalFilesReceived,
    int ValidCfdiCount,
    int InvalidXmlCount);

public sealed record GenerateCfdiPdfBatchCommand(
    IReadOnlyList<XmlFilePayload>? Files) : ICommand<CfdiPdfBatchResultDto>;

public sealed class GenerateCfdiPdfBatchCommandHandler
    : ICommandHandler<GenerateCfdiPdfBatchCommand, CfdiPdfBatchResultDto>
{
    public const int MaxFilesAllowed = 5000;

    private static readonly XNamespace Ns4 = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace Tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";

    public Task<Result<CfdiPdfBatchResultDto>> HandleAsync(
        GenerateCfdiPdfBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Files is null || command.Files.Count == 0)
        {
            return Task.FromResult(Result.Failure<CfdiPdfBatchResultDto>(Error.Validation(
                "Reports.NoFilesProvided",
                "Por favor, selecciona al menos un archivo XML.")));
        }

        if (command.Files.Count > MaxFilesAllowed)
        {
            return Task.FromResult(Result.Failure<CfdiPdfBatchResultDto>(Error.Validation(
                "Reports.TooManyFiles",
                $"Se permite procesar un máximo de {MaxFilesAllowed} archivos XML por lote.")));
        }

        var validCfdis = new List<Cfdi40Document>(command.Files.Count);
        var invalidCount = 0;

        foreach (var file in command.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parsed = TryReadComprobante40(file.Content);
            if (parsed is not null)
            {
                validCfdis.Add(parsed);
            }
            else
            {
                invalidCount++;
            }
        }

        if (validCfdis.Count == 0)
        {
            return Task.FromResult(Result.Failure<CfdiPdfBatchResultDto>(Error.Validation(
                "Reports.NoValidCfdiFiles",
                "No se subieron archivos XML CFDI 4.0 válidos.")));
        }

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 1;

            foreach (var fac in validCfdis)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var uuidPart = string.IsNullOrWhiteSpace(fac.Uuid) ? $"SinUUID_{index}" : fac.Uuid;
                var entryName = $"Reporte_{uuidPart}.pdf";
                if (!usedNames.Add(entryName))
                {
                    entryName = $"Reporte_{uuidPart}_{index}.pdf";
                    usedNames.Add(entryName);
                }

                var pdfBytes = BuildBasicPdfBytes(fac);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(pdfBytes, 0, pdfBytes.Length);
                index++;
            }
        }

        return Task.FromResult(Result.Success(new CfdiPdfBatchResultDto(
            ZipBytes: zipStream.ToArray(),
            TotalFilesReceived: command.Files.Count,
            ValidCfdiCount: validCfdis.Count,
            InvalidXmlCount: invalidCount)));
    }

    private static Cfdi40Document? TryReadComprobante40(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            var sanitized = new string(rawContent.Where(XmlConvert.IsXmlChar).ToArray());
            var startIndex = sanitized.IndexOf("<cfdi:Comprobante", StringComparison.Ordinal);
            if (startIndex > 0)
            {
                sanitized = sanitized[startIndex..];
            }

            var xDoc = XDocument.Parse(sanitized);
            var comp = xDoc.Descendants(Ns4 + "Comprobante").FirstOrDefault();
            if (comp is null)
            {
                return null;
            }

            var emisorNode = xDoc.Descendants(Ns4 + "Emisor").FirstOrDefault();
            var receptorNode = xDoc.Descendants(Ns4 + "Receptor").FirstOrDefault();
            var timbreNode = xDoc.Descendants(Tfd + "TimbreFiscalDigital").FirstOrDefault();

            var conceptos = xDoc.Descendants(Ns4 + "Concepto")
                .Select(c => new Cfdi40Concepto(
                    ClaveProdServ: Attr(c, "ClaveProdServ"),
                    Cantidad: Attr(c, "Cantidad"),
                    Descripcion: Attr(c, "Descripcion"),
                    ValorUnitario: Attr(c, "ValorUnitario"),
                    Importe: Attr(c, "Importe")))
                .ToList();

            return new Cfdi40Document(
                Version: Attr(comp, "Version"),
                Serie: Attr(comp, "Serie"),
                Folio: Attr(comp, "Folio"),
                Fecha: Attr(comp, "Fecha"),
                Subtotal: Attr(comp, "SubTotal"),
                Total: Attr(comp, "Total"),
                Moneda: Attr(comp, "Moneda"),
                Uuid: Attr(timbreNode, "UUID"),
                Emisor: emisorNode is null
                    ? null
                    : new Cfdi40Emisor(
                        Rfc: Attr(emisorNode, "Rfc"),
                        Nombre: Attr(emisorNode, "Nombre"),
                        RegimenFiscal: Attr(emisorNode, "RegimenFiscal")),
                Receptor: receptorNode is null
                    ? null
                    : new Cfdi40Receptor(
                        Rfc: Attr(receptorNode, "Rfc"),
                        Nombre: Attr(receptorNode, "Nombre"),
                        UsoCfdi: Attr(receptorNode, "UsoCFDI")),
                Conceptos: conceptos);
        }
        catch
        {
            return null;
        }
    }

    private static string Attr(XElement? element, string name) =>
        element?.Attribute(name)?.Value ?? string.Empty;

    private static byte[] BuildBasicPdfBytes(Cfdi40Document fac)
    {
        var lines = new List<(string Font, int Size, int Leading, string Text)>
        {
            ("/F2", 16, 22, $"Reporte CFDI {fac.Version}"),
            ("/F1", 10, 15, $"UUID: {fac.Uuid}"),
            ("/F1", 10, 15, $"Folio: {fac.Folio} | Serie: {fac.Serie}"),
            ("/F1", 10, 15, $"Fecha: {fac.Fecha}"),
            ("/F1", 10, 12, " ")
        };

        if (fac.Emisor is not null)
        {
            lines.Add(("/F2", 12, 18, "[Nodo Emisor]"));
            lines.Add(("/F1", 10, 14, $"RFC: {fac.Emisor.Rfc}"));
            lines.Add(("/F1", 10, 14, $"Nombre: {fac.Emisor.Nombre}"));
            lines.Add(("/F1", 10, 14, $"Regimen: {fac.Emisor.RegimenFiscal}"));
            lines.Add(("/F1", 10, 12, " "));
        }

        if (fac.Receptor is not null)
        {
            lines.Add(("/F2", 12, 18, "[Nodo Receptor]"));
            lines.Add(("/F1", 10, 14, $"RFC: {fac.Receptor.Rfc}"));
            lines.Add(("/F1", 10, 14, $"Nombre: {fac.Receptor.Nombre}"));
            lines.Add(("/F1", 10, 14, $"Uso CFDI: {fac.Receptor.UsoCfdi}"));
            lines.Add(("/F1", 10, 12, " "));
        }

        if (fac.Conceptos.Count > 0)
        {
            lines.Add(("/F2", 12, 18, "[Nodo Conceptos]"));
            foreach (var c in fac.Conceptos.Take(25))
            {
                lines.Add(("/F1", 10, 14, $"- Clave: {c.ClaveProdServ} | Cantidad: {c.Cantidad} | Desc: {c.Descripcion}"));
                lines.Add(("/F1", 10, 14, $"  Valor Unitario: {c.ValorUnitario} | Importe: {c.Importe}"));
            }
            lines.Add(("/F1", 10, 12, " "));
        }

        lines.Add(("/F2", 12, 18, "[Totales]"));
        lines.Add(("/F1", 10, 14, $"SubTotal: {fac.Subtotal}"));
        lines.Add(("/F1", 10, 14, $"Total: {fac.Total}"));
        lines.Add(("/F1", 10, 14, $"Moneda: {fac.Moneda}"));

        var contentBuilder = new StringBuilder();
        contentBuilder.Append("BT\n");
        var y = 800;
        foreach (var (font, size, leading, text) in lines)
        {
            if (y < 40)
            {
                break;
            }

            contentBuilder.Append(CultureInfo.InvariantCulture, $"{font} {size} Tf\n");
            contentBuilder.Append(CultureInfo.InvariantCulture, $"1 0 0 1 36 {y} Tm\n");
            contentBuilder.Append($"({EscapePdfString(text)}) Tj\n");
            y -= leading;
        }
        contentBuilder.Append("ET\n");

        var contentStream = contentBuilder.ToString();
        var contentByteLength = Encoding.ASCII.GetByteCount(contentStream);

        var objects = new string[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>\nendobj\n",
            $"4 0 obj\n<< /Length {contentByteLength} >>\nstream\n{contentStream}endstream\nendobj\n",
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            "6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n"
        };

        var pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");

        var offsets = new int[objects.Length + 1];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i + 1] = Encoding.ASCII.GetByteCount(pdf.ToString());
            pdf.Append(objects[i]);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n");
        pdf.Append("0000000000 65535 f \n");
        for (var i = 1; i <= objects.Length; i++)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{offsets[i]:D10} 00000 n \n");
        }

        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string EscapePdfString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            var normalized = ch switch
            {
                'á' or 'à' or 'ä' or 'â' => 'a',
                'Á' or 'À' or 'Ä' or 'Â' => 'A',
                'é' or 'è' or 'ë' or 'ê' => 'e',
                'É' or 'È' or 'Ë' or 'Ê' => 'E',
                'í' or 'ì' or 'ï' or 'î' => 'i',
                'Í' or 'Ì' or 'Ï' or 'Î' => 'I',
                'ó' or 'ò' or 'ö' or 'ô' => 'o',
                'Ó' or 'Ò' or 'Ö' or 'Ô' => 'O',
                'ú' or 'ù' or 'ü' or 'û' => 'u',
                'Ú' or 'Ù' or 'Ü' or 'Û' => 'U',
                'ñ' => 'n',
                'Ñ' => 'N',
                _ => ch
            };

            if (normalized is '\\' or '(' or ')')
            {
                sb.Append('\\');
            }

            sb.Append(normalized is >= ' ' and <= '~' ? normalized : ' ');
        }

        return sb.ToString();
    }

    private sealed record Cfdi40Document(
        string Version,
        string Serie,
        string Folio,
        string Fecha,
        string Subtotal,
        string Total,
        string Moneda,
        string Uuid,
        Cfdi40Emisor? Emisor,
        Cfdi40Receptor? Receptor,
        IReadOnlyList<Cfdi40Concepto> Conceptos);

    private sealed record Cfdi40Emisor(string Rfc, string Nombre, string RegimenFiscal);

    private sealed record Cfdi40Receptor(string Rfc, string Nombre, string UsoCfdi);

    private sealed record Cfdi40Concepto(
        string ClaveProdServ,
        string Cantidad,
        string Descripcion,
        string ValorUnitario,
        string Importe);
}

using System.IO.Compression;
using System.Text;
using Application.Features.Reports;
using Application.Features.Taxes.Dtos;
using Domain.Common;

namespace UnitTests.Application;

public sealed class ReportsHandlersTests
{
    private const string SampleCfdi40Xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4"
                          xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital"
                          Version="4.0" Serie="A" Folio="789" Fecha="2024-07-01T10:30:00"
                          SubTotal="1000.00" Total="1160.00" Moneda="MXN">
          <cfdi:Emisor Rfc="AAA010101AAA" Nombre="EMPRESA EMISORA SA DE CV" RegimenFiscal="601" />
          <cfdi:Receptor Rfc="XAXX010101000" Nombre="PUBLICO EN GENERAL" UsoCFDI="S01" />
          <cfdi:Conceptos>
            <cfdi:Concepto ClaveProdServ="84111506" Cantidad="1" ClaveUnidad="E48" Descripcion="Servicios de Facturacion" ValorUnitario="1000.00" Importe="1000.00" ObjetoImp="02" />
          </cfdi:Conceptos>
          <cfdi:Complemento>
            <tfd:TimbreFiscalDigital UUID="aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" FechaTimbrado="2024-07-01T10:35:00" RfcProvCertif="SAT970701NN3" />
          </cfdi:Complemento>
        </cfdi:Comprobante>
        """;

    [Fact]
    public async Task HandleGenerateCfdiPdfBatch_WhenNoFilesProvided_ShouldReturnValidationError()
    {
        // Arrange
        var handler = new GenerateCfdiPdfBatchCommandHandler();
        var command = new GenerateCfdiPdfBatchCommand([]);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Reports.NoFilesProvided", result.Error.Code);
    }

    [Fact]
    public async Task HandleGenerateCfdiPdfBatch_WhenAllXmlsAreInvalid_ShouldReturnValidationError()
    {
        // Arrange
        var handler = new GenerateCfdiPdfBatchCommandHandler();
        var command = new GenerateCfdiPdfBatchCommand(
        [
            new XmlFilePayload("invalido.xml", "<otro>no es cfdi 4.0</otro>")
        ]);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Reports.NoValidCfdiFiles", result.Error.Code);
    }

    [Fact]
    public async Task HandleGenerateCfdiPdfBatch_WithValidAndInvalidXmls_ShouldReturnZipWithPdfPerValidCfdi()
    {
        // Arrange
        var handler = new GenerateCfdiPdfBatchCommandHandler();
        var command = new GenerateCfdiPdfBatchCommand(
        [
            new XmlFilePayload("factura1.xml", SampleCfdi40Xml),
            new XmlFilePayload("corrupto.xml", "no-es-xml")
        ]);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalFilesReceived);
        Assert.Equal(1, result.Value.ValidCfdiCount);
        Assert.Equal(1, result.Value.InvalidXmlCount);
        Assert.NotEmpty(result.Value.ZipBytes);

        using var zipStream = new MemoryStream(result.Value.ZipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = Assert.Single(archive.Entries);
        Assert.Equal("Reporte_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.pdf", entry.FullName);

        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.ASCII);
        var pdfText = await reader.ReadToEndAsync();
        Assert.StartsWith("%PDF-1.4", pdfText);
        Assert.Contains("Reporte CFDI 4.0", pdfText);
        Assert.Contains("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", pdfText);
        Assert.Contains("EMPRESA EMISORA SA DE CV", pdfText);
        Assert.Contains("PUBLICO EN GENERAL", pdfText);
        Assert.Contains("Servicios de Facturacion", pdfText);
        Assert.Contains("%%EOF", pdfText);
    }
}

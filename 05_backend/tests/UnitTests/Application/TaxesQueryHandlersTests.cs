using Application.Common.Models;
using Application.Features.Taxes.Abstractions;
using Application.Features.Taxes.Commands;
using Application.Features.Taxes.Dtos;
using Application.Features.Taxes.Queries;
using Domain.Common;

namespace UnitTests.Application;

public sealed class TaxesQueryHandlersTests
{
    private readonly FakeTaxesReadRepository _repository = new();
    private readonly FakeTaxesWriteRepository _writeRepository = new();

    private const string SamplePayrollXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4"
                          xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital"
                          xmlns:nomina12="http://www.sat.gob.mx/nomina12"
                          Version="4.0" Serie="NOM" Folio="101" Fecha="2024-06-15T12:00:00"
                          SubTotal="15000.00" Descuento="2499.50" Total="12500.50"
                          Moneda="MXN" TipoDeComprobante="N" MetodoPago="PUE" LugarExpedicion="06600">
          <cfdi:Emisor Rfc="AAA010101AAA" Nombre="EMPRESA DEMO SA DE CV" RegimenFiscal="601" />
          <cfdi:Receptor Rfc="GODE561231GR8" Nombre="JUAN PEREZ" UsoCFDI="CN01" />
          <cfdi:Complemento>
            <tfd:TimbreFiscalDigital UUID="11111111-2222-3333-4444-555555555555" FechaTimbrado="2024-06-15T12:05:00" RfcProvCertif="SAT970701NN3" />
            <nomina12:Nomina Version="1.2" TipoNomina="O" FechaPago="2024-06-15" FechaInicialPago="2024-06-01" FechaFinalPago="2024-06-15" NumDiasPagados="15" TotalPercepciones="15000.00" TotalDeducciones="2499.50">
              <nomina12:Receptor NumEmpleado="EMP-001" Departamento="Tecnología" Puesto="Desarrollador Senior" TipoContrato="01" TipoRegimen="02" PeriodicidadPago="04" SalarioBaseCotApor="1000.00" SalarioDiarioIntegrado="1050.00" />
              <nomina12:Percepciones TotalGravado="14000.00" TotalExento="1000.00">
                <nomina12:Percepcion TipoPercepcion="001" Clave="P001" Concepto="Sueldo" ImporteGravado="14000.00" ImporteExento="1000.00" />
              </nomina12:Percepciones>
              <nomina12:Deducciones TotalImpuestosRetenidos="1500.00" TotalOtrasDeducciones="999.50">
                <nomina12:Deduccion TipoDeduccion="002" Clave="D001" Concepto="ISR" Importe="1500.00" />
                <nomina12:Deduccion TipoDeduccion="001" Clave="D002" Concepto="IMSS" Importe="999.50" />
              </nomina12:Deducciones>
            </nomina12:Nomina>
          </cfdi:Complemento>
        </cfdi:Comprobante>
        """;

    [Fact]
    public async Task HandleConsolidatedReport_WithValidFilter_ShouldNormalizeDatesAndReturnPagedResult()
    {
        // Arrange
        var handler = new GetConsolidatedReportQueryHandler(_repository);
        var startDate = new DateTime(2024, 1, 1, 14, 30, 0);
        var endDate = new DateTime(2024, 12, 31, 8, 15, 0);
        var query = new GetConsolidatedReportQuery(startDate, endDate, Page: 1, PageSize: 50);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(50, result.Value.PageSize);
        Assert.Equal(120, result.Value.TotalCount);
        Assert.Equal(3, result.Value.TotalPages);
        Assert.True(result.Value.HasNextPage);
        Assert.False(result.Value.HasPreviousPage);

        // Verifica que startDate se normalice a las 00:00:00 y endDate al final del día (23:59:59.9999999)
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0), _repository.LastStartDate);
        Assert.Equal(new DateTime(2024, 12, 31).Date.AddDays(1).AddTicks(-1), _repository.LastEndDate);
    }

    [Fact]
    public async Task HandleConsolidatedReport_WithSecondPage_ShouldReturnCorrectPaginationMetadata()
    {
        // Arrange
        var handler = new GetConsolidatedReportQueryHandler(_repository);
        var query = new GetConsolidatedReportQuery(
            new DateTime(2024, 1, 1),
            new DateTime(2024, 12, 31),
            Page: 2,
            PageSize: 50);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.Page);
        Assert.True(result.Value.HasPreviousPage);
        Assert.True(result.Value.HasNextPage);
    }

    [Fact]
    public async Task HandleConsolidatedReport_WhenStartDateIsGreaterThanEndDate_ShouldReturnValidationError()
    {
        // Arrange
        var handler = new GetConsolidatedReportQueryHandler(_repository);
        var invalidQuery = new GetConsolidatedReportQuery(
            StartDate: new DateTime(2025, 1, 1),
            EndDate: new DateTime(2024, 1, 1));

        // Act
        var result = await handler.HandleAsync(invalidQuery, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Taxes.InvalidDateRange", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleConsolidatedReport_WhenPageIsLessThanOne_ShouldReturnValidationError(int invalidPage)
    {
        // Arrange
        var handler = new GetConsolidatedReportQueryHandler(_repository);
        var query = new GetConsolidatedReportQuery(
            StartDate: new DateTime(2024, 1, 1),
            EndDate: new DateTime(2024, 12, 31),
            Page: invalidPage,
            PageSize: 50);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Pagination.InvalidPage", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(501)]
    public async Task HandleConsolidatedReport_WhenPageSizeIsOutOfRange_ShouldReturnValidationError(int invalidPageSize)
    {
        // Arrange
        var handler = new GetConsolidatedReportQueryHandler(_repository);
        var query = new GetConsolidatedReportQuery(
            StartDate: new DateTime(2024, 1, 1),
            EndDate: new DateTime(2024, 12, 31),
            Page: 1,
            PageSize: invalidPageSize);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Pagination.InvalidPageSize", result.Error.Code);
    }

    [Fact]
    public async Task HandleSeismicComparison_WithDefaultParameters_ShouldReturnDeterministicPointsAndMetrics()
    {
        // Arrange
        var handler = new GetSeismicComparisonQueryHandler();
        var query = new GetSeismicComparisonQuery(Count: 100, MinMagnitude: 1.0, Seed: 2026);

        // Act
        var firstRun = await handler.HandleAsync(query, CancellationToken.None);
        var secondRun = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(firstRun.IsSuccess);
        Assert.True(secondRun.IsSuccess);
        Assert.Equal(".NET 10 (Clean Architecture)", firstRun.Value.Engine);
        Assert.Equal(100, firstRun.Value.RequestedCount);
        Assert.Equal(100, firstRun.Value.ReturnedCount);
        Assert.Equal(100, firstRun.Value.Points.Count);
        Assert.Equal(2026, firstRun.Value.Seed);
        Assert.True(firstRun.Value.ServerElapsedMs >= 0);
        Assert.True(firstRun.Value.MemoryMegabytes > 0);
        Assert.All(firstRun.Value.Points, point =>
        {
            Assert.InRange(point.Lat, 14.0, 32.0);
            Assert.InRange(point.Lng, -118.0, -86.0);
            Assert.InRange(point.Mag, 1.0, 6.0);
        });

        // Determinismo: la misma semilla y entrada deben producir exactamente los mismos puntos y estadísticas
        Assert.Equal(firstRun.Value.AverageMagnitude, secondRun.Value.AverageMagnitude);
        Assert.Equal(firstRun.Value.MaxMagnitude, secondRun.Value.MaxMagnitude);
        Assert.Equal(firstRun.Value.Points, secondRun.Value.Points);
    }

    [Fact]
    public async Task HandleSeismicComparison_WithMinMagnitudeFilter_ShouldFilterPointsAndComputeStats()
    {
        // Arrange
        var handler = new GetSeismicComparisonQueryHandler();
        var query = new GetSeismicComparisonQuery(Count: 500, MinMagnitude: 4.5, Seed: 2026);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(500, result.Value.RequestedCount);
        Assert.True(result.Value.ReturnedCount > 0 && result.Value.ReturnedCount < 500);
        Assert.Equal(result.Value.ReturnedCount, result.Value.Points.Count);
        Assert.All(result.Value.Points, p => Assert.True(p.Mag >= 4.5));
        Assert.True(result.Value.AverageMagnitude >= 4.5);
        Assert.True(result.Value.MaxMagnitude >= result.Value.AverageMagnitude);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10001)]
    public async Task HandleSeismicComparison_WhenCountIsOutOfRange_ShouldReturnValidationError(int invalidCount)
    {
        // Arrange
        var handler = new GetSeismicComparisonQueryHandler();
        var query = new GetSeismicComparisonQuery(Count: invalidCount, MinMagnitude: 1.0, Seed: 2026);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Seismic.InvalidCount", result.Error.Code);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(10.1)]
    public async Task HandleSeismicComparison_WhenMinMagnitudeIsOutOfRange_ShouldReturnValidationError(double invalidMagnitude)
    {
        // Arrange
        var handler = new GetSeismicComparisonQueryHandler();
        var query = new GetSeismicComparisonQuery(Count: 100, MinMagnitude: invalidMagnitude, Seed: 2026);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Seismic.InvalidMagnitude", result.Error.Code);
    }

    [Fact]
    public async Task HandlePayrollSummary_WithReceipts_ShouldAggregateTotalsAndGroupCostByDepartment()
    {
        // Arrange
        var handler = new GetPayrollSummaryQueryHandler(_repository);
        var query = new GetPayrollSummaryQuery(
            StartDate: new DateTime(2024, 6, 1, 10, 0, 0),
            EndDate: new DateTime(2024, 6, 30, 18, 0, 0));

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalRecibos);
        Assert.Equal(25000.00m, result.Value.GranTotalPercepciones);
        Assert.Equal(4499.50m, result.Value.GranTotalDeducciones);
        Assert.Equal(20500.50m, result.Value.GranTotalNeto);
        Assert.Equal(2, result.Value.CostoPorDepartamento.Count);
        Assert.Contains(result.Value.CostoPorDepartamento, d => d.Departamento == "Tecnología" && d.TotalPercepciones == 15000.00m);
        Assert.Contains(result.Value.CostoPorDepartamento, d => d.Departamento == "Sin Departamento" && d.TotalPercepciones == 10000.00m);
        Assert.Equal(new DateTime(2024, 6, 1, 0, 0, 0), _repository.LastStartDate);
        Assert.Equal(new DateTime(2024, 6, 30).Date.AddDays(1).AddTicks(-1), _repository.LastEndDate);
    }

    [Fact]
    public async Task HandlePayrollSummary_WhenStartDateIsGreaterThanEndDate_ShouldReturnValidationError()
    {
        // Arrange
        var handler = new GetPayrollSummaryQueryHandler(_repository);
        var query = new GetPayrollSummaryQuery(
            StartDate: new DateTime(2025, 1, 1),
            EndDate: new DateTime(2024, 1, 1));

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Taxes.InvalidDateRange", result.Error.Code);
    }

    [Fact]
    public async Task HandleUploadBulkXml_WhenFilesListIsEmpty_ShouldReturnValidationError()
    {
        // Arrange
        var handler = new UploadBulkXmlCommandHandler(_writeRepository);
        var command = new UploadBulkXmlCommand([]);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Taxes.NoFilesProvided", result.Error.Code);
    }

    [Fact]
    public async Task HandleUploadBulkXml_WhenFilesExceedMaxLimit_ShouldReturnValidationError()
    {
        // Arrange
        var handler = new UploadBulkXmlCommandHandler(_writeRepository);
        var files = Enumerable.Range(1, UploadBulkXmlCommandHandler.MaxFilesAllowed + 1)
            .Select(i => new XmlFilePayload($"file_{i}.xml", SamplePayrollXml))
            .ToList();
        var command = new UploadBulkXmlCommand(files);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Taxes.TooManyFiles", result.Error.Code);
    }

    [Fact]
    public async Task HandleUploadBulkXml_WithValidAndInvalidXmls_ShouldParseSanitizeAndPersistValidRecords()
    {
        // Arrange
        var handler = new UploadBulkXmlCommandHandler(_writeRepository);
        var xmlWithUmlaut = SamplePayrollXml.Replace("JUAN PEREZ", "JÜAN PÉREZ");
        var invalidXml = "<not-a-cfdi>broken</not-a-cfdi>";
        var command = new UploadBulkXmlCommand(
        [
            new XmlFilePayload("nomina1.xml", xmlWithUmlaut),
            new XmlFilePayload("invalido.xml", invalidXml)
        ]);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.ReceivedCount);
        Assert.Equal(1, result.Value.ProcessedCount);
        Assert.Equal(1, result.Value.InvalidCount);
        var saved = Assert.Single(_writeRepository.SavedRecords);
        Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), saved.UUID);
        Assert.Equal("JUAN PÉREZ", saved.ReceptorNombre);
        Assert.NotNull(saved.Complemento);
        Assert.Equal("Nomina12", saved.Complemento!.TipoComplemento);
        Assert.NotNull(saved.Nomina);
        Assert.Equal("EMP-001", saved.Nomina!.NumEmpleado);
        Assert.Equal(15000.00m, saved.Nomina.TotalPercepciones);
        Assert.Equal(2499.50m, saved.Nomina.TotalDeducciones);
        Assert.Equal(3, saved.Nomina.Movimientos.Count);
    }

    [Fact]
    public async Task HandleUploadBulkXml_WhenAllXmlsAreInvalid_ShouldReturnValidationError()
    {
        // Arrange
        var handler = new UploadBulkXmlCommandHandler(_writeRepository);
        var command = new UploadBulkXmlCommand(
        [
            new XmlFilePayload("bad1.xml", "<invalid />"),
            new XmlFilePayload("bad2.xml", "not xml at all")
        ]);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Taxes.NoValidXmlFiles", result.Error.Code);
    }

    private sealed class FakeTaxesReadRepository : ITaxesReadRepository
    {
        public DateTime? LastStartDate { get; private set; }
        public DateTime? LastEndDate { get; private set; }

        public Task<PagedResult<ConsolidatedReportItemDto>> GetConsolidatedReportAsync(
            DateTime startDate,
            DateTime endDate,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            LastStartDate = startDate;
            LastEndDate = endDate;

            var item = new ConsolidatedReportItemDto(
                UUID: Guid.Parse("11111111-2222-3333-4444-555555555555"),
                EmisorRFC: "AAA010101AAA",
                ReceptorRFC: "XAXX010101000",
                Total: 12500.50m,
                Fecha: new DateTime(2024, 6, 15),
                TipoComplemento: "Nomina12",
                NumEmpleado: "EMP-001",
                Departamento: "Tecnología",
                Puesto: "Desarrollador Senior",
                TotalPercepciones: 15000.00m,
                TotalDeducciones: 2499.50m,
                TipoMovimiento: "Percepcion",
                Concepto: "Sueldo",
                Importe: 15000.00m);

            return Task.FromResult(new PagedResult<ConsolidatedReportItemDto>([item], page, pageSize, TotalCount: 120));
        }

        public Task<IReadOnlyList<PayrollReceiptItemDto>> GetPayrollReceiptsAsync(
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken cancellationToken = default)
        {
            LastStartDate = startDate;
            LastEndDate = endDate;

            IReadOnlyList<PayrollReceiptItemDto> items =
            [
                new PayrollReceiptItemDto(
                    UUID: Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    Fecha: new DateTime(2024, 6, 15),
                    ReceptorNombre: "Juan Pérez",
                    ReceptorRFC: "GODE561231GR8",
                    Total: 12500.50m,
                    FechaPago: new DateTime(2024, 6, 15),
                    TotalPercepciones: 15000.00m,
                    TotalDeducciones: 2499.50m,
                    NumEmpleado: "EMP-001",
                    Departamento: "Tecnología",
                    Puesto: "Desarrollador Senior"),
                new PayrollReceiptItemDto(
                    UUID: Guid.Parse("22222222-3333-4444-5555-666666666666"),
                    Fecha: new DateTime(2024, 6, 15),
                    ReceptorNombre: "Ana López",
                    ReceptorRFC: "LOPA900101AAA",
                    Total: 8000.00m,
                    FechaPago: new DateTime(2024, 6, 15),
                    TotalPercepciones: 10000.00m,
                    TotalDeducciones: 2000.00m,
                    NumEmpleado: "EMP-002",
                    Departamento: " ",
                    Puesto: "Analista")
            ];

            return Task.FromResult(items);
        }
    }

    private sealed class FakeTaxesWriteRepository : ITaxesWriteRepository
    {
        public List<ParsedCfdiCoreRecord> SavedRecords { get; } = [];

        public Task<int> UpsertBatchAsync(
            IReadOnlyList<ParsedCfdiCoreRecord> records,
            CancellationToken cancellationToken = default)
        {
            SavedRecords.AddRange(records);
            return Task.FromResult(records.Count);
        }
    }
}

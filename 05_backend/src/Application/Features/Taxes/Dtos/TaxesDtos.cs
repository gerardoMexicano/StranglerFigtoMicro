namespace Application.Features.Taxes.Dtos;

public sealed record ConsolidatedReportItemDto(
    Guid UUID,
    string EmisorRFC,
    string ReceptorRFC,
    decimal? Total,
    DateTime Fecha,
    string? TipoComplemento,
    string? NumEmpleado,
    string? Departamento,
    string? Puesto,
    decimal? TotalPercepciones,
    decimal? TotalDeducciones,
    string? TipoMovimiento,
    string? Concepto,
    decimal? Importe);

public sealed record SeismicPointDto(
    double Lat,
    double Lng,
    double Mag);

public sealed record SeismicComparisonResultDto(
    string Engine,
    int RequestedCount,
    int ReturnedCount,
    double MinMagnitude,
    int Seed,
    double AverageMagnitude,
    double MaxMagnitude,
    long ServerElapsedMs,
    double MemoryMegabytes,
    IReadOnlyList<SeismicPointDto> Points);

public sealed record XmlFilePayload(
    string FileName,
    string Content);

public sealed record BulkXmlUploadResultDto(
    int ReceivedCount,
    int ProcessedCount,
    int InvalidCount,
    long ElapsedMs,
    string Message);

public sealed record PayrollReceiptItemDto(
    Guid UUID,
    DateTime Fecha,
    string? ReceptorNombre,
    string ReceptorRFC,
    decimal Total,
    DateTime? FechaPago,
    decimal TotalPercepciones,
    decimal TotalDeducciones,
    string? NumEmpleado,
    string? Departamento,
    string? Puesto);

public sealed record DepartmentPayrollCostDto(
    string Departamento,
    decimal TotalPercepciones);

public sealed record PayrollSummaryReportDto(
    decimal GranTotalPercepciones,
    decimal GranTotalDeducciones,
    decimal GranTotalNeto,
    int TotalRecibos,
    IReadOnlyList<DepartmentPayrollCostDto> CostoPorDepartamento,
    IReadOnlyList<PayrollReceiptItemDto> Nominas);

public sealed record ParsedNominaMovimiento(
    string TipoMovimiento,
    string? TipoCodigo,
    string? Clave,
    string? Concepto,
    decimal ImporteGravado,
    decimal ImporteExento,
    decimal Importe);

public sealed record ParsedNominaResumen(
    string? TipoNomina,
    DateTime? FechaPago,
    DateTime? FechaInicialPago,
    DateTime? FechaFinalPago,
    decimal NumDiasPagados,
    decimal TotalPercepciones,
    decimal TotalDeducciones,
    decimal TotalGravado,
    decimal TotalExento,
    string NumEmpleado,
    string? Departamento,
    string? Puesto,
    string? TipoContrato,
    string? TipoRegimen,
    string? PeriodicidadPago,
    decimal SalarioBaseCotApor,
    decimal SalarioDiarioIntegrado,
    IReadOnlyList<ParsedNominaMovimiento> Movimientos);

public sealed record ParsedCfdiComplemento(
    string TipoComplemento,
    string JsonData,
    string XmlFragment);

public sealed record ParsedCfdiCoreRecord(
    Guid UUID,
    string TipoComprobante,
    string? Serie,
    string? Folio,
    DateTime Fecha,
    string? Moneda,
    decimal SubTotal,
    decimal Descuento,
    decimal Total,
    string? MetodoPago,
    string? LugarExpedicion,
    string EmisorRFC,
    string? EmisorNombre,
    string ReceptorRFC,
    string? ReceptorNombre,
    string? UsoCFDI,
    DateTime? FechaTimbrado,
    string? RfcProvCertif,
    string XmlOriginal,
    ParsedCfdiComplemento? Complemento,
    ParsedNominaResumen? Nomina);



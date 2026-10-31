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


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

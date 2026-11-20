namespace Application.Features.Dashboard.Dtos;

/// <summary>KPI Strip de Dashboard.aspx para un período.</summary>
public sealed record DashboardKpisDto(
    decimal Revenue,
    decimal GrossMarginPct,
    int Orders,
    decimal AverageTicket);

/// <summary>KPIs del período pedido y del período anterior de la misma duración.</summary>
public sealed record ExecutiveKpisDto(
    DashboardKpisDto Current,
    DashboardKpisDto Previous,
    DateTime PreviousStartDate,
    DateTime PreviousEndDate);

/// <summary>Visual 1: ingresos brutos, impuestos y total por mes.</summary>
public sealed record MonthlyRevenueDto(
    int Year,
    int Month,
    decimal GrossRevenue,
    decimal Taxes,
    decimal TotalRevenue);

/// <summary>Visual 2: margen neto por cliente.</summary>
public sealed record CustomerProfitDto(
    string Customer,
    decimal NetMargin);

/// <summary>Visual 3: margen bruto por grupo de stock.</summary>
public sealed record CategoryMarginDto(
    string Category,
    decimal GrossMargin);

/// <summary>Fila cruda del repositorio para el Visual 4.</summary>
public sealed record SalespersonSalesDto(
    string Salesperson,
    decimal Revenue,
    decimal MarginPct);

/// <summary>Visual 4: eficiencia del vendedor; LowMargin = margen menor a 20 %.</summary>
public sealed record SalespersonEfficiencyDto(
    string Salesperson,
    decimal Revenue,
    decimal MarginPct,
    bool LowMargin);

public sealed record ExecutiveDashboardDto(
    DateTime StartDate,
    DateTime EndDate,
    ExecutiveKpisDto Kpis,
    IReadOnlyList<MonthlyRevenueDto> MonthlyRevenue,
    IReadOnlyList<CustomerProfitDto> TopCustomers,
    IReadOnlyList<CategoryMarginDto> CategoryMargins,
    IReadOnlyList<SalespersonEfficiencyDto> Salespeople);

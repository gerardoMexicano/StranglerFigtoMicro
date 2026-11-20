using Application.Features.Dashboard.Dtos;

namespace Application.Features.Dashboard.Abstractions;

/// <summary>Consultas de agregados de WideWorldImporters que usaba /legacy/Dashboard.aspx.</summary>
public interface IDashboardReadRepository
{
    Task<DashboardKpisDto> GetKpisAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonthlyRevenueDto>> GetMonthlyRevenueAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerProfitDto>> GetTopCustomersAsync(
        DateTime startDate, DateTime endDate, int top, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryMarginDto>> GetCategoryMarginsAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalespersonSalesDto>> GetSalespeopleAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

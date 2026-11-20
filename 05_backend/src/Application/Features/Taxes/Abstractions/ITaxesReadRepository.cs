using Application.Common.Models;
using Application.Features.Taxes.Dtos;

namespace Application.Features.Taxes.Abstractions;

public interface ITaxesReadRepository
{
    Task<PagedResult<ConsolidatedReportItemDto>> GetConsolidatedReportAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollReceiptItemDto>> GetPayrollReceiptsAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
}

public interface ITaxesWriteRepository
{
    Task<int> UpsertBatchAsync(
        IReadOnlyList<ParsedCfdiCoreRecord> records,
        CancellationToken cancellationToken = default);
}

using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Taxes.Dtos;

namespace Application.Features.Taxes.Queries;

public sealed record GetConsolidatedReportQuery(
    DateTime StartDate,
    DateTime EndDate,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<ConsolidatedReportItemDto>>;

public sealed record GetSeismicComparisonQuery(
    int Count = 5000,
    double MinMagnitude = 1.0,
    int Seed = 2026) : IQuery<SeismicComparisonResultDto>;

public sealed record GetPayrollSummaryQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IQuery<PayrollSummaryReportDto>;



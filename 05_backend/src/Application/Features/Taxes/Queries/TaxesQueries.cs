using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Taxes.Dtos;

namespace Application.Features.Taxes.Queries;

public sealed record GetConsolidatedReportQuery(
    DateTime StartDate,
    DateTime EndDate,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<ConsolidatedReportItemDto>>;

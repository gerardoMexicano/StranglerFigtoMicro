using System.Diagnostics;
using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Taxes.Abstractions;
using Application.Features.Taxes.Dtos;
using Domain.Common;

namespace Application.Features.Taxes.Queries;

internal static class TaxesQueryValidation
{
    public const int MaxPageSize = 500;
    public const int MaxSeismicCount = 10_000;

    public static Error? Validate(DateTime startDate, DateTime endDate, int page, int pageSize)
    {
        if (startDate.Date > endDate.Date)
        {
            return Error.Validation(
                "Taxes.InvalidDateRange",
                "La fecha de inicio no puede ser mayor que la fecha final.");
        }

        if (page < 1)
        {
            return Error.Validation(
                "Pagination.InvalidPage",
                "El número de página debe ser mayor o igual a 1.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return Error.Validation(
                "Pagination.InvalidPageSize",
                $"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
        }

        return null;
    }

    public static Error? ValidateSeismic(int count, double minMagnitude)
    {
        if (count is < 1 or > MaxSeismicCount)
        {
            return Error.Validation(
                "Seismic.InvalidCount",
                $"La cantidad de sismos debe estar entre 1 y {MaxSeismicCount}.");
        }

        if (double.IsNaN(minMagnitude) || minMagnitude is < 0.0 or > 10.0)
        {
            return Error.Validation(
                "Seismic.InvalidMagnitude",
                "La magnitud mínima debe estar entre 0.0 y 10.0.");
        }

        return null;
    }

    public static (DateTime NormalizedStart, DateTime NormalizedEnd) NormalizeDates(DateTime startDate, DateTime endDate) =>
        (startDate.Date, endDate.Date.AddDays(1).AddTicks(-1));
}

public sealed class GetConsolidatedReportQueryHandler(ITaxesReadRepository repository)
    : IQueryHandler<GetConsolidatedReportQuery, PagedResult<ConsolidatedReportItemDto>>
{
    private readonly ITaxesReadRepository _repository = repository;

    public async Task<Result<PagedResult<ConsolidatedReportItemDto>>> HandleAsync(
        GetConsolidatedReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = TaxesQueryValidation.Validate(query.StartDate, query.EndDate, query.Page, query.PageSize);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<ConsolidatedReportItemDto>>(validationError);
        }

        var (start, end) = TaxesQueryValidation.NormalizeDates(query.StartDate, query.EndDate);
        var pageResult = await _repository.GetConsolidatedReportAsync(
            start, end, query.Page, query.PageSize, cancellationToken);

        return Result.Success(pageResult);
    }
}

public sealed class GetSeismicComparisonQueryHandler
    : IQueryHandler<GetSeismicComparisonQuery, SeismicComparisonResultDto>
{
    public const string EngineName = ".NET 10 (Clean Architecture)";

    public Task<Result<SeismicComparisonResultDto>> HandleAsync(
        GetSeismicComparisonQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationError = TaxesQueryValidation.ValidateSeismic(query.Count, query.MinMagnitude);
        if (validationError is not null)
        {
            return Task.FromResult(Result.Failure<SeismicComparisonResultDto>(validationError));
        }

        var sw = Stopwatch.StartNew();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        uint state = unchecked((uint)query.Seed);
        if (state == 0)
        {
            state = 2026u;
        }

        var points = new List<SeismicPointDto>(query.Count);
        double sumMag = 0;
        double maxMag = 0;

        for (int i = 0; i < query.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double rLat = NextUnitDouble(ref state);
            double rLng = NextUnitDouble(ref state);
            double rMag = NextUnitDouble(ref state);

            double lat = Math.Round(14.0 + (rLat * 18.0), 4);
            double lng = Math.Round(-118.0 + (rLng * 32.0), 4);
            double mag = Math.Round(1.0 + (rMag * 5.0), 2);

            if (mag >= query.MinMagnitude)
            {
                points.Add(new SeismicPointDto(lat, lng, mag));
                sumMag += mag;
                if (mag > maxMag)
                {
                    maxMag = mag;
                }
            }
        }

        double avgMag = points.Count > 0 ? Math.Round(sumMag / points.Count, 2) : 0.0;
        long allocatedBytes = Math.Max(1024L, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
        double memoryMb = Math.Max(0.01, Math.Round(allocatedBytes / (1024.0 * 1024.0), 2));
        sw.Stop();

        var result = new SeismicComparisonResultDto(
            Engine: EngineName,
            RequestedCount: query.Count,
            ReturnedCount: points.Count,
            MinMagnitude: query.MinMagnitude,
            Seed: query.Seed,
            AverageMagnitude: avgMag,
            MaxMagnitude: maxMag,
            ServerElapsedMs: sw.ElapsedMilliseconds,
            MemoryMegabytes: memoryMb,
            Points: points);

        return Task.FromResult(Result.Success(result));
    }

    // Generador determinista Xorshift32 idéntico en .NET Framework 4.8 y .NET 10
    private static double NextUnitDouble(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0x00FFFFFFu) / (double)0x01000000u;
    }
}



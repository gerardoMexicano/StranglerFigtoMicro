using Application.Common.Models;
using Application.Features.Taxes.Abstractions;
using Application.Features.Taxes.Dtos;
using Application.Features.Taxes.Queries;
using Domain.Common;

namespace UnitTests.Application;

public sealed class TaxesQueryHandlersTests
{
    private readonly FakeTaxesReadRepository _repository = new();

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

    private sealed class FakeTaxesReadRepository : ITaxesReadRepository
    {
        public DateTime LastStartDate { get; private set; }
        public DateTime LastEndDate { get; private set; }

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
    }
}

using Application.Common.Models;
using Application.Features.Sales.Abstractions;
using Application.Features.Sales.Dtos;
using Application.Features.Sales.Queries;
using Domain.Common;

namespace UnitTests.Application;

public sealed class SalesQueryHandlersTests
{
    private readonly FakeSalesReadRepository _repository = new();

    [Fact]
    public async Task HandleCustomerTransactions_WithValidFilter_ShouldNormalizeEndDateToEndOfDayAndReturnPagedResult()
    {
        // Arrange
        var handler = new GetCustomerTransactionsQueryHandler(_repository);
        var startDate = new DateTime(2013, 1, 1);
        var endDate = new DateTime(2016, 12, 31);
        var query = new GetCustomerTransactionsQuery(startDate, endDate, Page: 1, PageSize: 50);

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

        // Verifica que endDate se normalice al final del día (23:59:59.9999999) como en LEgFrontNEWBACK.aspx.vb
        Assert.Equal(new DateTime(2013, 1, 1, 0, 0, 0), _repository.LastStartDate);
        Assert.Equal(new DateTime(2016, 12, 31).Date.AddDays(1).AddTicks(-1), _repository.LastEndDate);
    }

    [Fact]
    public async Task HandleInvoiceLines_WithValidFilter_ShouldReturnPagedResult()
    {
        // Arrange
        var handler = new GetInvoiceLinesQueryHandler(_repository);
        var query = new GetInvoiceLinesQuery(new DateTime(2013, 1, 1), new DateTime(2016, 12, 31), Page: 2, PageSize: 50);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.Page);
        Assert.True(result.Value.HasPreviousPage);
    }

    [Fact]
    public async Task HandleInvoices_WithValidFilter_ShouldReturnPagedResult()
    {
        // Arrange
        var handler = new GetInvoicesQueryHandler(_repository);
        var query = new GetInvoicesQuery(new DateTime(2013, 1, 1), new DateTime(2016, 12, 31));

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(50, result.Value.PageSize);
    }

    [Fact]
    public async Task HandleOrderLines_WithValidFilter_ShouldReturnPagedResult()
    {
        // Arrange
        var handler = new GetOrderLinesQueryHandler(_repository);
        var query = new GetOrderLinesQuery(new DateTime(2013, 1, 1), new DateTime(2016, 12, 31));

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task HandleOrders_WithValidFilter_ShouldReturnPagedResult()
    {
        // Arrange
        var handler = new GetOrdersQueryHandler(_repository);
        var query = new GetOrdersQuery(new DateTime(2013, 1, 1), new DateTime(2016, 12, 31));

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task HandleSalesQueries_WhenStartDateIsGreaterThanEndDate_ShouldReturnValidationError()
    {
        // Arrange
        var handler = new GetCustomerTransactionsQueryHandler(_repository);
        var invalidQuery = new GetCustomerTransactionsQuery(
            StartDate: new DateTime(2017, 1, 1),
            EndDate: new DateTime(2016, 1, 1));

        // Act
        var result = await handler.HandleAsync(invalidQuery, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Sales.InvalidDateRange", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleSalesQueries_WhenPageIsLessThanOne_ShouldReturnValidationError(int invalidPage)
    {
        // Arrange
        var handler = new GetInvoicesQueryHandler(_repository);
        var query = new GetInvoicesQuery(
            StartDate: new DateTime(2013, 1, 1),
            EndDate: new DateTime(2016, 12, 31),
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
    public async Task HandleSalesQueries_WhenPageSizeIsOutOfRange_ShouldReturnValidationError(int invalidPageSize)
    {
        // Arrange
        var handler = new GetOrdersQueryHandler(_repository);
        var query = new GetOrdersQuery(
            StartDate: new DateTime(2013, 1, 1),
            EndDate: new DateTime(2016, 12, 31),
            Page: 1,
            PageSize: invalidPageSize);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Pagination.InvalidPageSize", result.Error.Code);
    }

    private sealed class FakeSalesReadRepository : ISalesReadRepository
    {
        public DateTime LastStartDate { get; private set; }
        public DateTime LastEndDate { get; private set; }

        public Task<PagedResult<CustomerTransactionDto>> GetCustomerTransactionsAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            LastStartDate = startDate;
            LastEndDate = endDate;
            var item = new CustomerTransactionDto(
                1, 10, 1, 100, 4, new DateTime(2015, 5, 10),
                1000m, 160m, 1160m, 0m, new DateTime(2015, 5, 11), true, 1, new DateTime(2015, 5, 10));
            return Task.FromResult(new PagedResult<CustomerTransactionDto>([item], page, pageSize, TotalCount: 120));
        }

        public Task<PagedResult<InvoiceLineDto>> GetInvoiceLinesAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            LastStartDate = startDate;
            LastEndDate = endDate;
            var item = new InvoiceLineDto(
                1, 100, 5, "Producto WWI", 7, 10, 25m, 16m, 40m, 80m, 290m, 1, new DateTime(2015, 5, 10));
            return Task.FromResult(new PagedResult<InvoiceLineDto>([item], page, pageSize, TotalCount: 120));
        }

        public Task<PagedResult<InvoiceDto>> GetInvoicesAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            LastStartDate = startDate;
            LastEndDate = endDate;
            var item = new InvoiceDto(
                100, 10, 10, 200, 3, 20, 21, 22, 23, new DateTime(2015, 5, 10),
                "PO-999", false, null, null, "Entregar en puerta", null, 5, 0,
                null, null, null, new DateTime(2015, 5, 10, 14, 0, 0), "Juan", 1, new DateTime(2015, 5, 10));
            return Task.FromResult(new PagedResult<InvoiceDto>([item], page, pageSize, TotalCount: 120));
        }

        public Task<PagedResult<OrderLineDto>> GetOrderLinesAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            LastStartDate = startDate;
            LastEndDate = endDate;
            var item = new OrderLineDto(
                1, 200, 5, "Producto WWI", 7, 10, 25m, 16m, 10, new DateTime(2015, 5, 10), 1, new DateTime(2015, 5, 10));
            return Task.FromResult(new PagedResult<OrderLineDto>([item], page, pageSize, TotalCount: 120));
        }

        public Task<PagedResult<OrderDto>> GetOrdersAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            LastStartDate = startDate;
            LastEndDate = endDate;
            var item = new OrderDto(
                200, 10, 22, 23, 20, null, new DateTime(2015, 5, 9), new DateTime(2015, 5, 10),
                "PO-999", true, null, "Entregar en puerta", null, new DateTime(2015, 5, 10), 1, new DateTime(2015, 5, 10));
            return Task.FromResult(new PagedResult<OrderDto>([item], page, pageSize, TotalCount: 120));
        }
    }
}

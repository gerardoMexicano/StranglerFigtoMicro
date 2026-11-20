using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Application.Features.Dashboard.Queries;
using Domain.Common;

namespace UnitTests.Application;

public sealed class DashboardQueryHandlerTests
{
    private readonly FakeDashboardReadRepository _repository = new();

    private GetExecutiveDashboardQueryHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_WithStartDateAfterEndDate_ShouldReturnValidationErrorWithoutQueryingRepository()
    {
        // Arrange
        var query = new GetExecutiveDashboardQuery(new DateTime(2016, 5, 31), new DateTime(2016, 1, 1));

        // Act
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Dashboard.InvalidDateRange", result.Error.Code);
        Assert.Empty(_repository.KpiCalls);
    }

    [Fact]
    public async Task Handle_WithSameStartAndEndDate_ShouldSucceed()
    {
        // Arrange: un solo día es un rango válido (BETWEEN inclusivo en Dashboard.aspx.vb)
        var query = new GetExecutiveDashboardQuery(new DateTime(2016, 3, 1), new DateTime(2016, 3, 1));

        // Act
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ShouldQueryCurrentAndPreviousPeriodOfSameLength_LikeCargarKPIs()
    {
        // Arrange: 2016-03-01..2016-03-31 son 31 días; el anterior es 2016-01-30..2016-02-29
        var query = new GetExecutiveDashboardQuery(new DateTime(2016, 3, 1), new DateTime(2016, 3, 31));

        // Act
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                (new DateTime(2016, 3, 1), new DateTime(2016, 3, 31)),
                (new DateTime(2016, 1, 30), new DateTime(2016, 2, 29))
            ],
            _repository.KpiCalls);
        Assert.Equal(new DateTime(2016, 1, 30), result.Value.Kpis.PreviousStartDate);
        Assert.Equal(new DateTime(2016, 2, 29), result.Value.Kpis.PreviousEndDate);
        Assert.Equal(_repository.CurrentKpis, result.Value.Kpis.Current);
        Assert.Equal(_repository.PreviousKpis, result.Value.Kpis.Previous);
    }

    [Fact]
    public async Task Handle_ShouldDropTimeOfDayFromFilter_BecauseInvoiceDateAndOrderDateAreDates()
    {
        // Arrange
        var query = new GetExecutiveDashboardQuery(
            new DateTime(2016, 3, 1, 14, 30, 0),
            new DateTime(2016, 3, 31, 8, 15, 0));

        // Act
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2016, 3, 1), result.Value.StartDate);
        Assert.Equal(new DateTime(2016, 3, 31), result.Value.EndDate);
        Assert.Equal((new DateTime(2016, 3, 1), new DateTime(2016, 3, 31)), _repository.KpiCalls[0]);
        Assert.Equal((new DateTime(2016, 3, 1), new DateTime(2016, 3, 31)), _repository.LastRange);
    }

    [Fact]
    public async Task Handle_ShouldAskRepositoryForTop10Customers()
    {
        // Arrange
        var query = new GetExecutiveDashboardQuery(new DateTime(2016, 1, 1), new DateTime(2016, 5, 31));

        // Act
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10, _repository.LastTop);
        Assert.Equal(_repository.Customers, result.Value.TopCustomers);
    }

    [Fact]
    public async Task Handle_ShouldReturnMonthlyRevenueAndCategoryMarginsAsReturnedByRepository()
    {
        // Arrange
        var query = new GetExecutiveDashboardQuery(new DateTime(2016, 1, 1), new DateTime(2016, 5, 31));

        // Act
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(_repository.Months, result.Value.MonthlyRevenue);
        Assert.Equal(_repository.Categories, result.Value.CategoryMargins);
    }

    [Theory]
    [InlineData(19.99, true)]
    [InlineData(20.00, false)]
    [InlineData(35.50, false)]
    [InlineData(0.00, true)]
    public async Task Handle_ShouldFlagSalespeopleWithMarginBelow20Percent(double marginPct, bool expectedLowMargin)
    {
        // Arrange: regla de Dashboard.aspx.vb (alerta = PctMargen < 20)
        _repository.Salespeople = [new SalespersonSalesDto("Kayla Woodcock", 1_250_000m, (decimal)marginPct)];
        var query = new GetExecutiveDashboardQuery(new DateTime(2016, 1, 1), new DateTime(2016, 5, 31));

        // Act
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var vendedor = Assert.Single(result.Value.Salespeople);
        Assert.Equal("Kayla Woodcock", vendedor.Salesperson);
        Assert.Equal(1_250_000m, vendedor.Revenue);
        Assert.Equal((decimal)marginPct, vendedor.MarginPct);
        Assert.Equal(expectedLowMargin, vendedor.LowMargin);
    }

    [Fact]
    public async Task Handle_ShouldKeepSalespeopleOrderFromRepository()
    {
        // Arrange: el repositorio ya ordena por revenue descendente
        _repository.Salespeople =
        [
            new SalespersonSalesDto("Archer Lamble", 900m, 30m),
            new SalespersonSalesDto("Hudson Onslow", 500m, 10m)
        ];
        var query = new GetExecutiveDashboardQuery(new DateTime(2016, 1, 1), new DateTime(2016, 5, 31));

        // Act
        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(["Archer Lamble", "Hudson Onslow"], result.Value.Salespeople.Select(s => s.Salesperson));
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellationTokenToRepository()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var query = new GetExecutiveDashboardQuery(new DateTime(2016, 1, 1), new DateTime(2016, 5, 31));

        // Act
        await CreateHandler().HandleAsync(query, cts.Token);

        // Assert
        Assert.All(_repository.Tokens, token => Assert.Equal(cts.Token, token));
        Assert.Equal(6, _repository.Tokens.Count);
    }

    private sealed class FakeDashboardReadRepository : IDashboardReadRepository
    {
        public DashboardKpisDto CurrentKpis { get; } = new(1_000_000m, 45.5m, 1200, 833.33m);
        public DashboardKpisDto PreviousKpis { get; } = new(800_000m, 44.1m, 1000, 800m);

        public IReadOnlyList<MonthlyRevenueDto> Months { get; } =
        [
            new MonthlyRevenueDto(2016, 1, 4_000_000m, 600_000m, 4_600_000m),
            new MonthlyRevenueDto(2016, 2, 3_500_000m, 525_000m, 4_025_000m)
        ];

        public IReadOnlyList<CustomerProfitDto> Customers { get; } =
        [
            new CustomerProfitDto("Tailspin Toys (Head Office)", 120_000m),
            new CustomerProfitDto("Wingtip Toys (Head Office)", 110_000m)
        ];

        public IReadOnlyList<CategoryMarginDto> Categories { get; } =
        [
            new CategoryMarginDto("Grupo 2", 300_000m),
            new CategoryMarginDto("Grupo 7", 150_000m)
        ];

        public IReadOnlyList<SalespersonSalesDto> Salespeople { get; set; } =
        [
            new SalespersonSalesDto("Kayla Woodcock", 1_250_000m, 42.1m)
        ];

        public List<(DateTime Start, DateTime End)> KpiCalls { get; } = [];
        public (DateTime Start, DateTime End) LastRange { get; private set; }
        public int LastTop { get; private set; }
        public List<CancellationToken> Tokens { get; } = [];

        public Task<DashboardKpisDto> GetKpisAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            KpiCalls.Add((startDate, endDate));
            return Task.FromResult(KpiCalls.Count == 1 ? CurrentKpis : PreviousKpis);
        }

        public Task<IReadOnlyList<MonthlyRevenueDto>> GetMonthlyRevenueAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            LastRange = (startDate, endDate);
            return Task.FromResult(Months);
        }

        public Task<IReadOnlyList<CustomerProfitDto>> GetTopCustomersAsync(
            DateTime startDate, DateTime endDate, int top, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            LastTop = top;
            return Task.FromResult(Customers);
        }

        public Task<IReadOnlyList<CategoryMarginDto>> GetCategoryMarginsAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(Categories);
        }

        public Task<IReadOnlyList<SalespersonSalesDto>> GetSalespeopleAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return Task.FromResult(Salespeople);
        }
    }
}

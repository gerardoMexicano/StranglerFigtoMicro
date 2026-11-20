using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Api;

public sealed class DashboardEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Url = "/api/dashboard/executive?startDate=2016-01-01&endDate=2016-05-31";

    private readonly WebApplicationFactory<Program> _factory;

    public DashboardEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=Laboratory;User Id=app_lector;Password=Test1234!;TrustServerCertificate=True",
                    ["ConnectionStrings:WideWorldImporters"] = "Server=localhost,1433;Database=WideWorldImporters;User Id=app_lector;Password=Test1234!;TrustServerCertificate=True",
                    ["Oidc:Authority"] = "http://localhost/auth/realms/laboratorio",
                    ["Oidc:MetadataAddress"] = "http://keycloak:8080/auth/realms/laboratorio/.well-known/openid-configuration",
                    ["Oidc:Audience"] = "backend-api",
                    ["Oidc:RequireHttpsMetadata"] = "false"
                });
            });
        });
    }

    [Fact]
    public async Task GetExecutiveDashboard_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    [InlineData("invitado")]
    public async Task GetExecutiveDashboard_WithAnyReadRole_ShouldReturn200Ok(string role)
    {
        // Arrange
        using var client = CreateAuthenticatedClient(new StubDashboardReadRepository(), role);

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetExecutiveDashboard_WithoutRealmRoles_ShouldReturn403Forbidden()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(new StubDashboardReadRepository());

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetExecutiveDashboard_WithInvalidDateRange_ShouldReturn400ProblemDetails()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(new StubDashboardReadRepository(), "usuario");

        // Act
        var response = await client.GetAsync("/api/dashboard/executive?startDate=2016-05-31&endDate=2016-01-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetExecutiveDashboard_WithAuthenticatedUser_ShouldReturnAllFiveVisualsInCamelCase()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(new StubDashboardReadRepository(), "usuario");

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("2016-01-01T00:00:00", root.GetProperty("startDate").GetString());
        Assert.Equal("2016-05-31T00:00:00", root.GetProperty("endDate").GetString());
        Assert.Equal(1_000_000m, root.GetProperty("kpis").GetProperty("current").GetProperty("revenue").GetDecimal());
        Assert.Equal(800_000m, root.GetProperty("kpis").GetProperty("previous").GetProperty("revenue").GetDecimal());
        Assert.Equal(2, root.GetProperty("monthlyRevenue").GetArrayLength());
        Assert.Equal("Tailspin Toys (Head Office)", root.GetProperty("topCustomers")[0].GetProperty("customer").GetString());
        Assert.Equal("Grupo 2", root.GetProperty("categoryMargins")[0].GetProperty("category").GetString());
        var vendedor = root.GetProperty("salespeople")[0];
        Assert.Equal("Hudson Onslow", vendedor.GetProperty("salesperson").GetString());
        Assert.True(vendedor.GetProperty("lowMargin").GetBoolean());
    }

    [Fact]
    public async Task GetExecutiveDashboard_WithoutDates_ShouldUseLastYearWithDataInWideWorldImporters()
    {
        // Arrange: WWI llega hasta 2016-05-31; el default de Dashboard.aspx (año en curso) saldría vacío
        var repository = new StubDashboardReadRepository();
        using var client = CreateAuthenticatedClient(repository, "usuario");

        // Act
        var response = await client.GetAsync("/api/dashboard/executive");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((new DateTime(2016, 1, 1), new DateTime(2016, 5, 31)), repository.FirstKpiRange);
    }

    private HttpClient CreateAuthenticatedClient(StubDashboardReadRepository repository, params string[] roles)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDashboardReadRepository>();
                services.AddSingleton<IDashboardReadRepository>(repository);

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization = roles.Length == 0
            ? new AuthenticationHeaderValue(TestAuthHandler.SchemeName)
            : new AuthenticationHeaderValue(TestAuthHandler.SchemeName, string.Join(',', roles));
        return client;
    }

    private sealed class StubDashboardReadRepository : IDashboardReadRepository
    {
        public (DateTime Start, DateTime End)? FirstKpiRange { get; private set; }

        public Task<DashboardKpisDto> GetKpisAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var esActual = FirstKpiRange is null;
            FirstKpiRange ??= (startDate, endDate);
            return Task.FromResult(esActual
                ? new DashboardKpisDto(1_000_000m, 45.5m, 1200, 833.33m)
                : new DashboardKpisDto(800_000m, 44.1m, 1000, 800m));
        }

        public Task<IReadOnlyList<MonthlyRevenueDto>> GetMonthlyRevenueAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MonthlyRevenueDto>>(
            [
                new MonthlyRevenueDto(2016, 1, 4_000_000m, 600_000m, 4_600_000m),
                new MonthlyRevenueDto(2016, 2, 3_500_000m, 525_000m, 4_025_000m)
            ]);

        public Task<IReadOnlyList<CustomerProfitDto>> GetTopCustomersAsync(
            DateTime startDate, DateTime endDate, int top, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerProfitDto>>(
                [new CustomerProfitDto("Tailspin Toys (Head Office)", 120_000m)]);

        public Task<IReadOnlyList<CategoryMarginDto>> GetCategoryMarginsAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategoryMarginDto>>([new CategoryMarginDto("Grupo 2", 300_000m)]);

        public Task<IReadOnlyList<SalespersonSalesDto>> GetSalespeopleAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SalespersonSalesDto>>(
                [new SalespersonSalesDto("Hudson Onslow", 500_000m, 12.5m)]);
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestDashboardBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var roles = (header.Parameter ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", "kc-user-001"),
                new Claim("preferred_username", "juan"),
                new Claim("realm_access", JsonSerializer.Serialize(new { roles }))
            ], SchemeName);

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

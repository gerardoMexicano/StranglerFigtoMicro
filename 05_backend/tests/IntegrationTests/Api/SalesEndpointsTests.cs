using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Common.Models;
using Application.Features.Sales.Abstractions;
using Application.Features.Sales.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Api;

public sealed class SalesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SalesEndpointsTests(WebApplicationFactory<Program> factory)
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

    [Theory]
    [InlineData("/api/sales/customer-transactions?startDate=2013-01-01&endDate=2016-12-31")]
    [InlineData("/api/sales/invoice-lines?startDate=2013-01-01&endDate=2016-12-31")]
    [InlineData("/api/sales/invoices?startDate=2013-01-01&endDate=2016-12-31")]
    [InlineData("/api/sales/order-lines?startDate=2013-01-01&endDate=2016-12-31")]
    [InlineData("/api/sales/orders?startDate=2013-01-01&endDate=2016-12-31")]
    public async Task GetSalesEndpoints_WithoutBearerToken_ShouldReturn401Unauthorized(string url)
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomerTransactions_WithInvalidDateRange_ShouldReturn400BadRequest()
    {
        // Arrange
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/sales/customer-transactions?startDate=2016-12-31&endDate=2013-01-01&page=1&pageSize=50");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/sales/customer-transactions?startDate=2013-01-01&endDate=2016-12-31&page=1&pageSize=50")]
    [InlineData("/api/sales/invoice-lines?startDate=2013-01-01&endDate=2016-12-31&page=1&pageSize=50")]
    [InlineData("/api/sales/invoices?startDate=2013-01-01&endDate=2016-12-31&page=1&pageSize=50")]
    [InlineData("/api/sales/order-lines?startDate=2013-01-01&endDate=2016-12-31&page=1&pageSize=50")]
    [InlineData("/api/sales/orders?startDate=2013-01-01&endDate=2016-12-31&page=1&pageSize=50")]
    public async Task GetAllFiveSalesEndpoints_WithAuthenticatedUser_ShouldReturn200OkAndPagedPayload(string url)
    {
        // Arrange
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<PagedResult<object>>();
        Assert.NotNull(json);
        Assert.Equal(1, json.Page);
        Assert.Equal(50, json.PageSize);
        Assert.Equal(1, json.TotalCount);
        Assert.Single(json.Items);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISalesReadRepository>();
                services.AddScoped<ISalesReadRepository, StubSalesReadRepository>();

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.SchemeName);
        return client;
    }

    private sealed class StubSalesReadRepository : ISalesReadRepository
    {
        public Task<PagedResult<CustomerTransactionDto>> GetCustomerTransactionsAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<CustomerTransactionDto>(
            [
                new CustomerTransactionDto(1, 10, 1, 100, 4, startDate, 100m, 16m, 116m, 0m, endDate, true, 1, startDate)
            ], page, pageSize, 1));

        public Task<PagedResult<InvoiceLineDto>> GetInvoiceLinesAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<InvoiceLineDto>(
            [
                new InvoiceLineDto(1, 100, 5, "Item WWI", 7, 2, 50m, 16m, 16m, 20m, 116m, 1, startDate)
            ], page, pageSize, 1));

        public Task<PagedResult<InvoiceDto>> GetInvoicesAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<InvoiceDto>(
            [
                new InvoiceDto(100, 10, 10, 200, 3, 20, 21, 22, 23, startDate, "PO-1", false, null, null, null, null, 1, 0, null, null, null, null, null, 1, startDate)
            ], page, pageSize, 1));

        public Task<PagedResult<OrderLineDto>> GetOrderLinesAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<OrderLineDto>(
            [
                new OrderLineDto(1, 200, 5, "Item WWI", 7, 2, 50m, 16m, 2, startDate, 1, startDate)
            ], page, pageSize, 1));

        public Task<PagedResult<OrderDto>> GetOrdersAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<OrderDto>(
            [
                new OrderDto(200, 10, 22, 23, 20, null, startDate, endDate, "PO-1", false, null, null, null, startDate, 1, startDate)
            ], page, pageSize, 1));
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestSalesBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
            [
                new Claim("sub", "kc-user-001"),
                new Claim("preferred_username", "juan"),
                new Claim("realm_access", """{"roles":["usuario"]}""")
            ], SchemeName);

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

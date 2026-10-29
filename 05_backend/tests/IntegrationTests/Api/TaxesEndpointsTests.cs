using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.Common.Models;
using Application.Features.Taxes.Abstractions;
using Application.Features.Taxes.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Api;

public sealed class TaxesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TaxesEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=Laboratory;User Id=app_lector;Password=Test1234!;TrustServerCertificate=True",
                    ["ConnectionStrings:WideWorldImporters"] = "Server=localhost,1433;Database=WideWorldImporters;User Id=app_lector;Password=Test1234!;TrustServerCertificate=True",
                    ["ConnectionStrings:TaxesProto"] = "Server=localhost,1433;Database=taxes_proto;User Id=app_lector;Password=Test1234!;TrustServerCertificate=True",
                    ["Oidc:Authority"] = "http://localhost/auth/realms/laboratorio",
                    ["Oidc:MetadataAddress"] = "http://keycloak:8080/auth/realms/laboratorio/.well-known/openid-configuration",
                    ["Oidc:Audience"] = "backend-api",
                    ["Oidc:RequireHttpsMetadata"] = "false"
                });
            });
        });
    }

    [Fact]
    public async Task GetConsolidatedReport_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/taxes/consolidated-report?startDate=2024-01-01&endDate=2024-12-31");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConsolidatedReport_WithInvalidDateRange_ShouldReturn400BadRequest()
    {
        // Arrange
        using var client = CreateAuthenticatedClient("usuario");

        // Act
        var response = await client.GetAsync("/api/taxes/consolidated-report?startDate=2025-12-31&endDate=2024-01-01&page=1&pageSize=50");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/taxes/consolidated-report?startDate=2024-01-01&endDate=2024-12-31&page=0&pageSize=50")]
    [InlineData("/api/taxes/consolidated-report?startDate=2024-01-01&endDate=2024-12-31&page=1&pageSize=501")]
    public async Task GetConsolidatedReport_WithInvalidPagination_ShouldReturn400BadRequest(string url)
    {
        // Arrange
        using var client = CreateAuthenticatedClient("usuario");

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetConsolidatedReport_WithAuthenticatedUser_ShouldReturn200OkAndPagedPayload()
    {
        // Arrange
        using var client = CreateAuthenticatedClient("usuario");

        // Act
        var response = await client.GetAsync("/api/taxes/consolidated-report?startDate=2024-01-01&endDate=2024-12-31&page=1&pageSize=50");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<PagedResult<ConsolidatedReportItemDto>>();
        Assert.NotNull(json);
        Assert.Equal(1, json.Page);
        Assert.Equal(50, json.PageSize);
        Assert.Equal(1, json.TotalCount);
        var item = Assert.Single(json.Items);
        Assert.Equal("AAA010101AAA", item.EmisorRFC);
        Assert.Equal("Nomina12", item.TipoComplemento);
        Assert.Equal("EMP-001", item.NumEmpleado);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    [InlineData("invitado")]
    public async Task GetConsolidatedReport_WithAnyReadRole_ShouldReturn200Ok(string role)
    {
        // Arrange
        using var client = CreateAuthenticatedClient(role);

        // Act
        var response = await client.GetAsync("/api/taxes/consolidated-report?startDate=2024-01-01&endDate=2024-12-31");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConsolidatedReport_WithoutRealmRoles_ShouldReturn403Forbidden()
    {
        // Arrange
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/taxes/consolidated-report?startDate=2024-01-01&endDate=2024-12-31");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetConsolidatedReport_WithUnknownRole_ShouldReturn403Forbidden()
    {
        // Arrange
        using var client = CreateAuthenticatedClient("otro-rol");

        // Act
        var response = await client.GetAsync("/api/taxes/consolidated-report?startDate=2024-01-01&endDate=2024-12-31");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient() => CreateAuthenticatedClient([]);

    private HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaxesReadRepository>();
                services.AddScoped<ITaxesReadRepository, StubTaxesReadRepository>();

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

    private sealed class StubTaxesReadRepository : ITaxesReadRepository
    {
        public Task<PagedResult<ConsolidatedReportItemDto>> GetConsolidatedReportAsync(
            DateTime startDate,
            DateTime endDate,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<ConsolidatedReportItemDto>(
            [
                new ConsolidatedReportItemDto(
                    UUID: Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    EmisorRFC: "AAA010101AAA",
                    ReceptorRFC: "XAXX010101000",
                    Total: 12500.50m,
                    Fecha: startDate,
                    TipoComplemento: "Nomina12",
                    NumEmpleado: "EMP-001",
                    Departamento: "Tecnología",
                    Puesto: "Desarrollador Senior",
                    TotalPercepciones: 15000.00m,
                    TotalDeducciones: 2499.50m,
                    TipoMovimiento: "Percepcion",
                    Concepto: "Sueldo",
                    Importe: 15000.00m)
            ], page, pageSize, 1));
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestTaxesBearer";

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

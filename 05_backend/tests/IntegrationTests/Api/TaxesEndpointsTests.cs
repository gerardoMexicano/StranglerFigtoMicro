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

    [Fact]
    public async Task GetSeismicGeoJson_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/taxes/seismic-geojson?count=100&minMagnitude=1.0&seed=2026");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/taxes/seismic-geojson?count=0&minMagnitude=1.0&seed=2026")]
    [InlineData("/api/taxes/seismic-geojson?count=10001&minMagnitude=1.0&seed=2026")]
    [InlineData("/api/taxes/seismic-geojson?count=100&minMagnitude=-1.0&seed=2026")]
    [InlineData("/api/taxes/seismic-geojson?count=100&minMagnitude=10.5&seed=2026")]
    public async Task GetSeismicGeoJson_WithInvalidParameters_ShouldReturn400BadRequest(string url)
    {
        // Arrange
        using var client = CreateAuthenticatedClient("usuario");

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    [InlineData("invitado")]
    public async Task GetSeismicGeoJson_WithAnyReadRole_ShouldReturn200OkAndDeterministicPayload(string role)
    {
        // Arrange
        using var client = CreateAuthenticatedClient(role);

        // Act
        var response = await client.GetAsync("/api/taxes/seismic-geojson?count=50&minMagnitude=2.0&seed=2026");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SeismicComparisonResultDto>();
        Assert.NotNull(payload);
        Assert.Equal(50, payload.RequestedCount);
        Assert.Equal(2026, payload.Seed);
        Assert.Equal(payload.ReturnedCount, payload.Points.Count);
        Assert.All(payload.Points, p => Assert.True(p.Mag >= 2.0));
    }

    [Fact]
    public async Task GetSeismicGeoJson_WithoutRealmRoles_ShouldReturn403Forbidden()
    {
        // Arrange
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/taxes/seismic-geojson?count=50&minMagnitude=1.0&seed=2026");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPayrollSummary_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/taxes/payroll-summary?startDate=2024-01-01&endDate=2024-12-31");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    [InlineData("invitado")]
    public async Task GetPayrollSummary_WithAnyReadRole_ShouldReturn200OkAndSummaryPayload(string role)
    {
        using var client = CreateAuthenticatedClient(role);
        var response = await client.GetAsync("/api/taxes/payroll-summary?startDate=2024-01-01&endDate=2024-12-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PayrollSummaryReportDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.TotalRecibos);
        Assert.Equal(15000.00m, payload.GranTotalPercepciones);
        Assert.Equal(2499.50m, payload.GranTotalDeducciones);
        Assert.Equal(12500.50m, payload.GranTotalNeto);
    }

    [Fact]
    public async Task UploadXmlBatch_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        using var client = _factory.CreateClient();
        using var content = CreateMultipartXmlContent(("nomina1.xml", SamplePayrollXml));
        var response = await client.PostAsync("/api/taxes/xml-batch", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadXmlBatch_WithInvitadoRole_ShouldReturn403Forbidden()
    {
        using var client = CreateAuthenticatedClient("invitado");
        using var content = CreateMultipartXmlContent(("nomina1.xml", SamplePayrollXml));
        var response = await client.PostAsync("/api/taxes/xml-batch", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    public async Task UploadXmlBatch_WithWriteRole_ShouldReturn200OkAndProcessedSummary(string role)
    {
        using var client = CreateAuthenticatedClient(role);
        using var content = CreateMultipartXmlContent(("nomina1.xml", SamplePayrollXml));
        var response = await client.PostAsync("/api/taxes/xml-batch", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<BulkXmlUploadResultDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.ReceivedCount);
        Assert.Equal(1, payload.ProcessedCount);
        Assert.Equal(0, payload.InvalidCount);
    }

    [Fact]
    public async Task UploadXmlBatch_WithoutFiles_ShouldReturn400BadRequest()
    {
        using var client = CreateAuthenticatedClient("usuario");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("empty"), "note");
        var response = await client.PostAsync("/api/taxes/xml-batch", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MultipartFormDataContent CreateMultipartXmlContent(params (string FileName, string Xml)[] files)
    {
        var content = new MultipartFormDataContent();
        foreach (var (fileName, xml) in files)
        {
            var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(xml));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(fileContent, "xmlFiles", fileName);
        }
        return content;
    }

    private const string SamplePayrollXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4"
                          xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital"
                          xmlns:nomina12="http://www.sat.gob.mx/nomina12"
                          Version="4.0" Serie="NOM" Folio="101" Fecha="2024-06-15T12:00:00"
                          SubTotal="15000.00" Descuento="2499.50" Total="12500.50"
                          Moneda="MXN" TipoDeComprobante="N" MetodoPago="PUE" LugarExpedicion="06600">
          <cfdi:Emisor Rfc="AAA010101AAA" Nombre="EMPRESA DEMO SA DE CV" RegimenFiscal="601" />
          <cfdi:Receptor Rfc="GODE561231GR8" Nombre="JUAN PEREZ" UsoCFDI="CN01" />
          <cfdi:Complemento>
            <tfd:TimbreFiscalDigital UUID="11111111-2222-3333-4444-555555555555" FechaTimbrado="2024-06-15T12:05:00" RfcProvCertif="SAT970701NN3" />
            <nomina12:Nomina Version="1.2" TipoNomina="O" FechaPago="2024-06-15" FechaInicialPago="2024-06-01" FechaFinalPago="2024-06-15" NumDiasPagados="15" TotalPercepciones="15000.00" TotalDeducciones="2499.50">
              <nomina12:Receptor NumEmpleado="EMP-001" Departamento="Tecnología" Puesto="Desarrollador Senior" TipoContrato="01" TipoRegimen="02" PeriodicidadPago="04" SalarioBaseCotApor="1000.00" SalarioDiarioIntegrado="1050.00" />
            </nomina12:Nomina>
          </cfdi:Complemento>
        </cfdi:Comprobante>
        """;

    private HttpClient CreateAuthenticatedClient() => CreateAuthenticatedClient([]);

    private HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaxesReadRepository>();
                services.AddScoped<ITaxesReadRepository, StubTaxesReadRepository>();
                services.RemoveAll<ITaxesWriteRepository>();
                services.AddScoped<ITaxesWriteRepository, StubTaxesWriteRepository>();

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

        public Task<IReadOnlyList<PayrollReceiptItemDto>> GetPayrollReceiptsAsync(
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PayrollReceiptItemDto>>(
            [
                new PayrollReceiptItemDto(
                    UUID: Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    Fecha: new DateTime(2024, 6, 15),
                    ReceptorNombre: "Juan Pérez",
                    ReceptorRFC: "XAXX010101000",
                    Total: 12500.50m,
                    FechaPago: new DateTime(2024, 6, 15),
                    TotalPercepciones: 15000.00m,
                    TotalDeducciones: 2499.50m,
                    NumEmpleado: "EMP-001",
                    Departamento: "Tecnología",
                    Puesto: "Desarrollador Senior")
            ]);
    }

    private sealed class StubTaxesWriteRepository : ITaxesWriteRepository
    {
        public Task<int> UpsertBatchAsync(
            IReadOnlyList<ParsedCfdiCoreRecord> records,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(records.Count);
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

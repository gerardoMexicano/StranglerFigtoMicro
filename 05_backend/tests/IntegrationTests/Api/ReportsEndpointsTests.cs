using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Api;

public sealed class ReportsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ReportsEndpointsTests(WebApplicationFactory<Program> factory)
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
    public async Task GenerateCfdiPdfBatch_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        using var client = _factory.CreateClient();
        using var content = CreateMultipartXmlContent(("cfdi.xml", SampleCfdi40Xml));
        var response = await client.PostAsync("/api/reports/cfdi-pdf-batch", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenerateCfdiPdfBatch_WithoutRealmRoles_ShouldReturn403Forbidden()
    {
        using var client = CreateAuthenticatedClient();
        using var content = CreateMultipartXmlContent(("cfdi.xml", SampleCfdi40Xml));
        var response = await client.PostAsync("/api/reports/cfdi-pdf-batch", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    [InlineData("invitado")]
    public async Task GenerateCfdiPdfBatch_WithAnyReadRole_ShouldReturn200OkZipWithGeneratedPdfAndCountHeaders(string role)
    {
        using var client = CreateAuthenticatedClient(role);
        using var content = CreateMultipartXmlContent(
            ("factura1.xml", SampleCfdi40Xml),
            ("corrupto.xml", "no-es-xml"));

        var response = await client.PostAsync("/api/reports/cfdi-pdf-batch", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("2", response.Headers.GetValues("X-Received-Count").Single());
        Assert.Equal("1", response.Headers.GetValues("X-Valid-Count").Single());
        Assert.Equal("1", response.Headers.GetValues("X-Invalid-Count").Single());

        var zipBytes = await response.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = Assert.Single(archive.Entries);
        Assert.Equal("Reporte_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.pdf", entry.FullName);
    }

    [Fact]
    public async Task GenerateCfdiPdfBatch_WithoutFiles_ShouldReturn400BadRequest()
    {
        using var client = CreateAuthenticatedClient("usuario");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("empty"), "note");
        var response = await client.PostAsync("/api/reports/cfdi-pdf-batch", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateCfdiPdfBatch_WithMoreFilesThanDefaultFormValueCountLimit_ShouldReturn200Ok()
    {
        // FormOptions.ValueCountLimit vale 1024 por defecto y también cuenta archivos:
        // sin ampliarlo, un lote grande truena (500) antes de llegar al handler.
        const int fileCount = 1500;
        using var client = CreateAuthenticatedClient("usuario");
        var files = Enumerable.Range(1, fileCount)
            .Select(i => ($"factura{i}.xml", SampleCfdi40Xml))
            .ToArray();
        using var content = CreateMultipartXmlContent(files);

        var response = await client.PostAsync("/api/reports/cfdi-pdf-batch", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(fileCount.ToString(), response.Headers.GetValues("X-Received-Count").Single());
    }

    private static MultipartFormDataContent CreateMultipartXmlContent(params (string FileName, string Xml)[] files)
    {
        var content = new MultipartFormDataContent();
        foreach (var (fileName, xml) in files)
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(fileContent, "xmlFiles", fileName);
        }
        return content;
    }

    private const string SampleCfdi40Xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4"
                          xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital"
                          Version="4.0" Serie="A" Folio="789" Fecha="2024-07-01T10:30:00"
                          SubTotal="1000.00" Total="1160.00" Moneda="MXN">
          <cfdi:Emisor Rfc="AAA010101AAA" Nombre="EMPRESA EMISORA SA DE CV" RegimenFiscal="601" />
          <cfdi:Receptor Rfc="XAXX010101000" Nombre="PUBLICO EN GENERAL" UsoCFDI="S01" />
          <cfdi:Conceptos>
            <cfdi:Concepto ClaveProdServ="84111506" Cantidad="1" ClaveUnidad="E48" Descripcion="Servicios de Facturacion" ValorUnitario="1000.00" Importe="1000.00" ObjetoImp="02" />
          </cfdi:Conceptos>
          <cfdi:Complemento>
            <tfd:TimbreFiscalDigital UUID="aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" FechaTimbrado="2024-07-01T10:35:00" RfcProvCertif="SAT970701NN3" />
          </cfdi:Complemento>
        </cfdi:Comprobante>
        """;

    private HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
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

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestReportsBearer";

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

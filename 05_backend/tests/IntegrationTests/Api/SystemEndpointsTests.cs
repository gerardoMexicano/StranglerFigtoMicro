using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Features.Users.GetCurrentUser;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Api;

public sealed class SystemEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SystemEndpointsTests(WebApplicationFactory<Program> factory)
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
    public async Task GetHealth_WithoutAuthentication_ShouldReturn200Ok()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/system/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidKeycloakClaims_ShouldReturn200OkAndMappedRoles()
    {
        // Arrange
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestKeycloakAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestKeycloakAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestKeycloakAuthHandler>(
                    TestKeycloakAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestKeycloakAuthHandler.SchemeName);

        // Act
        var response = await client.GetAsync("/api/system/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(user);
        Assert.Equal("kc-user-001", user.UserId);
        Assert.Equal("juan", user.UserName);
        Assert.Equal("juan@laboratorio.local", user.Email);
        Assert.Contains("admin", user.Roles);
        Assert.Contains("usuario", user.Roles);
    }

    private sealed class TestKeycloakAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestKeycloakBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            const string realmAccessJson = """{"roles":["admin","usuario"]}""";
            var claims = new[]
            {
                new Claim("sub", "kc-user-001"),
                new Claim("preferred_username", "juan"),
                new Claim("email", "juan@laboratorio.local"),
                new Claim("realm_access", realmAccessJson)
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

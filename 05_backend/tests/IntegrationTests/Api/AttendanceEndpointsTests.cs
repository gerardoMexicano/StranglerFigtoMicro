using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.Features.Attendance.Abstractions;
using Application.Features.Attendance.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Api;

public sealed class AttendanceEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CheckInsUrl = "/api/attendance/check-ins";
    private const string MineUrl = "/api/attendance/check-ins/mine";

    private readonly WebApplicationFactory<Program> _factory;

    public AttendanceEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=Laboratory;User Id=app_escritor;Password=Test1234!;TrustServerCertificate=True",
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
    public async Task PostCheckIn_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(CheckInsUrl, ValidBody);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_WithoutBearerToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(MineUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    public async Task PostCheckIn_WithUsuarioOrAdmin_ShouldReturn201AndSaveTheTokenUser(string role)
    {
        // Arrange
        var repository = new StubCheckInRepository();
        using var client = CreateAuthenticatedClient(repository, role);

        // Act
        var response = await client.PostAsJsonAsync(CheckInsUrl, ValidBody);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckInDto>();
        Assert.NotNull(body);
        Assert.Equal(19.432608m, body.Latitude);
        Assert.Equal(-99.133209m, body.Longitude);

        var saved = Assert.Single(repository.Added);
        Assert.Equal("kc-user-001", saved.UserId);
        Assert.Equal("juan", saved.UserName);
    }

    [Fact]
    public async Task PostCheckIn_WithInvitado_ShouldReturn403Forbidden()
    {
        // Arrange
        var repository = new StubCheckInRepository();
        using var client = CreateAuthenticatedClient(repository, "invitado");

        // Act
        var response = await client.PostAsJsonAsync(CheckInsUrl, ValidBody);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task PostCheckIn_WithoutLatitude_ShouldReturn400BadRequest()
    {
        // Arrange
        var repository = new StubCheckInRepository();
        using var client = CreateAuthenticatedClient(repository, "usuario");

        // Act
        var response = await client.PostAsJsonAsync(CheckInsUrl, new { longitude = -99.133209, accuracyMeters = 12.5 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    [InlineData("invitado")]
    public async Task GetMine_WithAnyReadRole_ShouldReturn200AndOnlyTheTokenUser(string role)
    {
        // Arrange
        var repository = new StubCheckInRepository();
        using var client = CreateAuthenticatedClient(repository, role);

        // Act
        var response = await client.GetAsync($"{MineUrl}?take=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<CheckInDto>>();
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal(("kc-user-001", 5), repository.LastQuery);
    }

    [Fact]
    public async Task GetMine_WithoutRealmRoles_ShouldReturn403Forbidden()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(new StubCheckInRepository());

        // Act
        var response = await client.GetAsync(MineUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static readonly object ValidBody = new
    {
        latitude = 19.432608,
        longitude = -99.133209,
        accuracyMeters = 12.5
    };

    // Mismo esquema que SalesEndpointsTests: los roles viajan en el encabezado
    // ("TestAttendanceBearer usuario") y llegan en realm_access, como los manda Keycloak.
    private HttpClient CreateAuthenticatedClient(StubCheckInRepository repository, params string[] roles)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICheckInRepository>();
                services.AddSingleton<ICheckInRepository>(repository);

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

    private sealed class StubCheckInRepository : ICheckInRepository
    {
        public List<NewCheckIn> Added { get; } = [];

        public (string UserId, int Take)? LastQuery { get; private set; }

        public Task<CheckInDto> AddAsync(NewCheckIn checkIn, CancellationToken cancellationToken = default)
        {
            Added.Add(checkIn);
            return Task.FromResult(new CheckInDto(
                Added.Count, checkIn.Latitude, checkIn.Longitude, checkIn.AccuracyMeters, DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyList<CheckInDto>> GetRecentByUserAsync(
            string userId, int take, CancellationToken cancellationToken = default)
        {
            LastQuery = (userId, take);
            return Task.FromResult<IReadOnlyList<CheckInDto>>(
            [
                new CheckInDto(7, 19.432608m, -99.133209m, 12.5m, DateTimeOffset.UtcNow)
            ]);
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestAttendanceBearer";

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

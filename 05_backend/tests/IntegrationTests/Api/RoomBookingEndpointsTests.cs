using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.Features.RoomBookings.Abstractions;
using Application.Features.RoomBookings.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Api;

public sealed class RoomBookingEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string RoomsUrl = "/api/rooms";
    private const string BookingsUrl = "/api/rooms/bookings";

    // Lejos en el futuro para que la regla "no en el pasado" no dependa del día en que corre la prueba
    private static readonly object ValidBody = new
    {
        roomId = 1,
        startsAt = "2099-01-15T09:00:00",
        endsAt = "2099-01-15T10:30:00",
        subject = "Junta de planeación"
    };

    private readonly WebApplicationFactory<Program> _factory;

    public RoomBookingEndpointsTests(WebApplicationFactory<Program> factory)
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

    [Theory]
    [InlineData("GET", RoomsUrl)]
    [InlineData("GET", BookingsUrl + "?date=2099-01-15")]
    [InlineData("POST", BookingsUrl)]
    [InlineData("DELETE", BookingsUrl + "/1")]
    public async Task AnyEndpoint_WithoutBearerToken_ShouldReturn401Unauthorized(string method, string url)
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(ValidBody);
        }

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    [InlineData("invitado")]
    public async Task GetRoomsAndBookings_WithAnyReadRole_ShouldReturn200(string role)
    {
        // Arrange
        var repository = new StubRoomBookingRepository();
        using var client = CreateAuthenticatedClient(repository, role);

        // Act
        var rooms = await client.GetFromJsonAsync<List<RoomDto>>(RoomsUrl);
        var bookings = await client.GetFromJsonAsync<List<BookingDto>>($"{BookingsUrl}?date=2099-01-15");

        // Assert
        Assert.NotNull(rooms);
        Assert.Equal(2, rooms.Count);
        Assert.NotNull(bookings);
        var booking = Assert.Single(bookings);
        Assert.Equal(new DateTime(2099, 1, 15, 12, 0, 0), booking.StartsAt);
        Assert.Equal(new DateOnly(2099, 1, 15), repository.LastDay);
    }

    [Theory]
    [InlineData(BookingsUrl)]
    [InlineData(BookingsUrl + "?date=mañana")]
    public async Task GetBookings_WithoutValidDate_ShouldReturn400BadRequest(string url)
    {
        // Arrange
        using var client = CreateAuthenticatedClient(new StubRoomBookingRepository(), "usuario");

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostBooking_WithUnreadableJson_ShouldReturn400BadRequest()
    {
        // Arrange
        var repository = new StubRoomBookingRepository();
        using var client = CreateAuthenticatedClient(repository, "usuario");

        // Act
        var response = await client.PostAsync(
            BookingsUrl, new StringContent("{ \"roomId\": ", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData("usuario")]
    [InlineData("admin")]
    public async Task PostBooking_WithUsuarioOrAdmin_ShouldReturn201AndSaveTheTokenUser(string role)
    {
        // Arrange
        var repository = new StubRoomBookingRepository();
        using var client = CreateAuthenticatedClient(repository, role);

        // Act
        var response = await client.PostAsJsonAsync(BookingsUrl, ValidBody);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(body);
        Assert.True(body.IsMine);
        Assert.Equal(new DateTime(2099, 1, 15, 9, 0, 0), body.StartsAt);

        var saved = Assert.Single(repository.Added);
        Assert.Equal("kc-user-001", saved.UserId);
        Assert.Equal("juan", saved.UserName);
        Assert.Equal(new DateTime(2099, 1, 15, 10, 30, 0), saved.EndsAt);
    }

    [Fact]
    public async Task PostBooking_WithInvitado_ShouldReturn403Forbidden()
    {
        // Arrange
        var repository = new StubRoomBookingRepository();
        using var client = CreateAuthenticatedClient(repository, "invitado");

        // Act
        var response = await client.PostAsJsonAsync(BookingsUrl, ValidBody);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task PostBooking_WithOffsetInTheTime_ShouldReturn400BadRequest()
    {
        // Arrange
        var repository = new StubRoomBookingRepository();
        using var client = CreateAuthenticatedClient(repository, "usuario");

        // Act
        var response = await client.PostAsJsonAsync(BookingsUrl, new
        {
            roomId = 1,
            startsAt = "2099-01-15T09:00:00Z",
            endsAt = "2099-01-15T10:00:00Z",
            subject = "Revisión"
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task PostBooking_WhenTheSlotIsTaken_ShouldReturn409Conflict()
    {
        // Arrange
        var repository = new StubRoomBookingRepository { SlotIsTaken = true };
        using var client = CreateAuthenticatedClient(repository, "usuario");

        // Act
        var response = await client.PostAsJsonAsync(BookingsUrl, ValidBody);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Rooms.Overlap", problem.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task DeleteBooking_OwnBooking_ShouldReturn204()
    {
        // Arrange
        var repository = new StubRoomBookingRepository();
        using var client = CreateAuthenticatedClient(repository, "usuario");

        // Act
        var response = await client.DeleteAsync($"{BookingsUrl}/{StubRoomBookingRepository.JuansBookingId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([StubRoomBookingRepository.JuansBookingId], repository.Deleted);
    }

    [Theory]
    [InlineData("usuario", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.NoContent)]
    public async Task DeleteBooking_SomeoneElsesBooking_OnlyAdminCanCancelIt(string role, HttpStatusCode expected)
    {
        // Arrange
        var repository = new StubRoomBookingRepository();
        using var client = CreateAuthenticatedClient(repository, role);

        // Act
        var response = await client.DeleteAsync($"{BookingsUrl}/{StubRoomBookingRepository.AnasBookingId}");

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(expected == HttpStatusCode.NoContent ? 1 : 0, repository.Deleted.Count);
    }

    [Fact]
    public async Task DeleteBooking_WithInvitado_ShouldReturn403Forbidden()
    {
        // Arrange
        var repository = new StubRoomBookingRepository();
        using var client = CreateAuthenticatedClient(repository, "invitado");

        // Act
        var response = await client.DeleteAsync($"{BookingsUrl}/{StubRoomBookingRepository.JuansBookingId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(repository.Deleted);
    }

    [Fact]
    public async Task DeleteBooking_Unknown_ShouldReturn404NotFound()
    {
        // Arrange
        using var client = CreateAuthenticatedClient(new StubRoomBookingRepository(), "usuario");

        // Act
        var response = await client.DeleteAsync($"{BookingsUrl}/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Mismo esquema que AttendanceEndpointsTests: los roles viajan en el encabezado
    // ("TestRoomsBearer usuario") y llegan en realm_access, como los manda Keycloak.
    private HttpClient CreateAuthenticatedClient(StubRoomBookingRepository repository, params string[] roles)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRoomBookingRepository>();
                services.AddSingleton<IRoomBookingRepository>(repository);

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.SchemeName, string.Join(',', roles));
        return client;
    }

    private sealed class StubRoomBookingRepository : IRoomBookingRepository
    {
        public const long JuansBookingId = 10;
        public const long AnasBookingId = 11;

        private readonly List<BookingRecord> _bookings =
        [
            new(JuansBookingId, 1, new DateTime(2099, 1, 15, 12, 0, 0), new DateTime(2099, 1, 15, 13, 0, 0), "Revisión", "kc-user-001", "juan"),
            new(AnasBookingId, 2, new DateTime(2099, 1, 16, 12, 0, 0), new DateTime(2099, 1, 16, 13, 0, 0), "Capacitación", "kc-user-002", "ana")
        ];

        public bool SlotIsTaken { get; init; }

        public List<NewBooking> Added { get; } = [];

        public List<long> Deleted { get; } = [];

        public DateOnly? LastDay { get; private set; }

        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoomDto>>(
            [
                new RoomDto(1, "Sala Juárez", 8, "Piso 1"),
                new RoomDto(2, "Sala Hidalgo", 12, "Piso 2")
            ]);

        public Task<bool> RoomExistsAsync(int roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult(roomId is 1 or 2);

        public Task<IReadOnlyList<BookingRecord>> GetBookingsByDayAsync(
            DateOnly day, CancellationToken cancellationToken = default)
        {
            LastDay = day;
            return Task.FromResult<IReadOnlyList<BookingRecord>>(
                _bookings.Where(b => DateOnly.FromDateTime(b.StartsAt) == day).ToList());
        }

        public Task<BookingRecord?> GetBookingAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_bookings.SingleOrDefault(b => b.Id == id));

        public Task<BookingRecord?> AddIfFreeAsync(NewBooking booking, CancellationToken cancellationToken = default)
        {
            if (SlotIsTaken)
            {
                return Task.FromResult<BookingRecord?>(null);
            }

            Added.Add(booking);
            return Task.FromResult<BookingRecord?>(new BookingRecord(
                100 + Added.Count, booking.RoomId, booking.StartsAt, booking.EndsAt,
                booking.Subject, booking.UserId, booking.UserName));
        }

        public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            Deleted.Add(id);
            return Task.CompletedTask;
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestRoomsBearer";

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

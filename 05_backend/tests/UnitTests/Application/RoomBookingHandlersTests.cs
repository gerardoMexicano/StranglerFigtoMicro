using System.Globalization;
using Application.Common.Abstractions;
using Application.Features.RoomBookings.Abstractions;
using Application.Features.RoomBookings.Commands;
using Application.Features.RoomBookings.Dtos;
using Application.Features.RoomBookings.Queries;
using Domain.Common;

namespace UnitTests.Application;

public sealed class RoomBookingHandlersTests
{
    // "Ahora" en todas las pruebas: jueves 30/10/2026 a las 10:00, hora local de las salas
    private static readonly DateTime Now = new(2026, 10, 30, 10, 0, 0);
    private static readonly FixedTimeProvider Clock = new(Now);

    private static readonly FakeCurrentUserService Juan = new(
        IsAuthenticated: true,
        UserId: "kc-user-001",
        UserName: "juan",
        Roles: ["usuario"]);

    private static readonly FakeCurrentUserService Ana = new(
        IsAuthenticated: true,
        UserId: "kc-user-002",
        UserName: "ana",
        Roles: ["usuario"]);

    private static readonly FakeCurrentUserService Admin = new(
        IsAuthenticated: true,
        UserId: "kc-admin-001",
        UserName: "admin",
        Roles: ["admin"]);

    [Fact]
    public async Task CreateBooking_WithFreeSlot_ShouldSaveItForTheTokenUser()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        var handler = new CreateBookingCommandHandler(repository, Juan, Clock);

        // Act
        var result = await handler.HandleAsync(new CreateBookingCommand(
            1, At("2026-10-31 09:00"), At("2026-10-31 10:30"), "  Junta de planeación  "));

        // Assert
        Assert.True(result.IsSuccess);
        var saved = Assert.Single(repository.Added);
        Assert.Equal(
            new NewBooking(1, At("2026-10-31 09:00"), At("2026-10-31 10:30"), "Junta de planeación", "kc-user-001", "juan"),
            saved);
        Assert.True(result.Value.IsMine);
        Assert.True(result.Value.CanCancel);
    }

    [Fact]
    public async Task CreateBooking_LaterToday_ShouldBeValid()
    {
        // Arrange
        var handler = new CreateBookingCommandHandler(new FakeRoomBookingRepository(), Juan, Clock);

        // Act
        var result = await handler.HandleAsync(new CreateBookingCommand(
            1, At("2026-10-30 10:30"), At("2026-10-30 11:00"), "Revisión"));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("2026-10-31 08:00", "2026-10-31 20:00")]
    [InlineData("2026-10-31 19:30", "2026-10-31 20:00")]
    public async Task CreateBooking_OnTheOpeningHours_ShouldBeValid(string startsAt, string endsAt)
    {
        // Arrange
        var handler = new CreateBookingCommandHandler(new FakeRoomBookingRepository(), Juan, Clock);

        // Act
        var result = await handler.HandleAsync(new CreateBookingCommand(1, At(startsAt), At(endsAt), "Revisión"));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("2026-10-31 09:15", "2026-10-31 10:00", "Rooms.ScheduleNotOnSlot")]
    [InlineData("2026-10-31 09:00", "2026-10-31 09:45", "Rooms.ScheduleNotOnSlot")]
    [InlineData("2026-10-31 10:00", "2026-10-31 10:00", "Rooms.ScheduleInverted")]
    [InlineData("2026-10-31 11:00", "2026-10-31 10:00", "Rooms.ScheduleInverted")]
    [InlineData("2026-10-31 07:30", "2026-10-31 09:00", "Rooms.ScheduleOutOfHours")]
    [InlineData("2026-10-31 19:00", "2026-10-31 20:30", "Rooms.ScheduleOutOfHours")]
    [InlineData("2026-10-31 19:00", "2026-11-01 09:00", "Rooms.ScheduleOutOfHours")]
    [InlineData("2026-10-30 09:00", "2026-10-30 11:00", "Rooms.ScheduleInThePast")]
    [InlineData("2026-10-30 10:00", "2026-10-30 11:00", "Rooms.ScheduleInThePast")]
    public async Task CreateBooking_WithInvalidSchedule_ShouldReturnValidationErrorAndNotSave(
        string startsAt, string endsAt, string expectedCode)
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        var handler = new CreateBookingCommandHandler(repository, Juan, Clock);

        // Act
        var result = await handler.HandleAsync(new CreateBookingCommand(1, At(startsAt), At(endsAt), "Revisión"));

        // Assert
        AssertValidation(result, expectedCode);
        Assert.Empty(repository.Added);
    }

    public static TheoryData<CreateBookingCommand, string> MissingOrMalformedData => new()
    {
        { new CreateBookingCommand(null, At("2026-10-31 09:00"), At("2026-10-31 10:00"), "Revisión"), "Rooms.RoomRequired" },
        { new CreateBookingCommand(1, At("2026-10-31 09:00"), At("2026-10-31 10:00"), null), "Rooms.InvalidSubject" },
        { new CreateBookingCommand(1, At("2026-10-31 09:00"), At("2026-10-31 10:00"), "   "), "Rooms.InvalidSubject" },
        { new CreateBookingCommand(1, At("2026-10-31 09:00"), At("2026-10-31 10:00"), new string('x', 121)), "Rooms.InvalidSubject" },
        { new CreateBookingCommand(1, null, At("2026-10-31 10:00"), "Revisión"), "Rooms.ScheduleRequired" },
        { new CreateBookingCommand(1, At("2026-10-31 09:00"), null, "Revisión"), "Rooms.ScheduleRequired" },
        {
            new CreateBookingCommand(
                1,
                DateTime.SpecifyKind(At("2026-10-31 09:00"), DateTimeKind.Utc),
                DateTime.SpecifyKind(At("2026-10-31 10:00"), DateTimeKind.Utc),
                "Revisión"),
            "Rooms.ScheduleWithOffset"
        },
    };

    [Theory]
    [MemberData(nameof(MissingOrMalformedData))]
    public async Task CreateBooking_WithMissingOrMalformedData_ShouldReturnValidationErrorAndNotSave(
        CreateBookingCommand command, string expectedCode)
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        var handler = new CreateBookingCommandHandler(repository, Juan, Clock);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        AssertValidation(result, expectedCode);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateBooking_WithUnknownRoom_ShouldReturnNotFoundAndNotSave()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        var handler = new CreateBookingCommandHandler(repository, Juan, Clock);

        // Act
        var result = await handler.HandleAsync(new CreateBookingCommand(
            99, At("2026-10-31 09:00"), At("2026-10-31 10:00"), "Revisión"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CreateBooking_WhenTheSlotIsTaken_ShouldReturnConflict()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository { SlotIsTaken = true };
        var handler = new CreateBookingCommandHandler(repository, Juan, Clock);

        // Act
        var result = await handler.HandleAsync(new CreateBookingCommand(
            1, At("2026-10-31 09:00"), At("2026-10-31 10:00"), "Revisión"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Rooms.Overlap", result.Error.Code);
    }

    [Fact]
    public async Task CreateBooking_WithoutUserId_ShouldReturnUnauthorizedAndNotSave()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        var handler = new CreateBookingCommandHandler(repository, Juan with { UserId = "" }, Clock);

        // Act
        var result = await handler.HandleAsync(new CreateBookingCommand(
            1, At("2026-10-31 09:00"), At("2026-10-31 10:00"), "Revisión"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task GetBookingsByDay_ShouldMarkWhichOnesTheUserCanCancel()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        repository.Bookings.Add(BookingOf("kc-user-001", "juan", id: 1));
        repository.Bookings.Add(BookingOf("kc-user-002", "ana", id: 2));

        // Act
        var deJuan = await new GetBookingsByDayQueryHandler(repository, Juan)
            .HandleAsync(new GetBookingsByDayQuery(new DateOnly(2026, 10, 31)));
        var deAdmin = await new GetBookingsByDayQueryHandler(repository, Admin)
            .HandleAsync(new GetBookingsByDayQuery(new DateOnly(2026, 10, 31)));

        // Assert
        Assert.Equal(new DateOnly(2026, 10, 31), repository.LastDay);
        Assert.Collection(
            deJuan.Value,
            mine => Assert.True(mine.IsMine && mine.CanCancel),
            other => Assert.False(other.IsMine || other.CanCancel));
        Assert.All(deAdmin.Value, b => Assert.True(!b.IsMine && b.CanCancel));
    }

    [Fact]
    public async Task CancelBooking_OwnBooking_ShouldDeleteIt()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        repository.Bookings.Add(BookingOf("kc-user-001", "juan", id: 5));

        // Act
        var result = await new CancelBookingCommandHandler(repository, Juan).HandleAsync(new CancelBookingCommand(5));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([5L], repository.Deleted);
    }

    [Fact]
    public async Task CancelBooking_SomeoneElsesBooking_ShouldReturnForbiddenAndNotDelete()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        repository.Bookings.Add(BookingOf("kc-user-001", "juan", id: 5));

        // Act
        var result = await new CancelBookingCommandHandler(repository, Ana).HandleAsync(new CancelBookingCommand(5));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Empty(repository.Deleted);
    }

    [Fact]
    public async Task CancelBooking_AsAdmin_ShouldDeleteAnyBooking()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();
        repository.Bookings.Add(BookingOf("kc-user-001", "juan", id: 5));

        // Act
        var result = await new CancelBookingCommandHandler(repository, Admin).HandleAsync(new CancelBookingCommand(5));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([5L], repository.Deleted);
    }

    [Fact]
    public async Task CancelBooking_Unknown_ShouldReturnNotFound()
    {
        // Arrange
        var repository = new FakeRoomBookingRepository();

        // Act
        var result = await new CancelBookingCommandHandler(repository, Juan).HandleAsync(new CancelBookingCommand(5));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Empty(repository.Deleted);
    }

    private static DateTime At(string value) =>
        DateTime.ParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static BookingRecord BookingOf(string userId, string userName, long id) =>
        new(id, 1, At("2026-10-31 09:00"), At("2026-10-31 10:00"), "Revisión", userId, userName);

    private static void AssertValidation(Result result, string expectedCode)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    /// <summary>Un reloj fijo: GetLocalNow() devuelve siempre la misma hora local.</summary>
    private sealed class FixedTimeProvider(DateTime localNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(localNow, TimeSpan.Zero);
    }

    private sealed class FakeRoomBookingRepository : IRoomBookingRepository
    {
        public bool SlotIsTaken { get; init; }

        public List<BookingRecord> Bookings { get; } = [];

        public List<NewBooking> Added { get; } = [];

        public List<long> Deleted { get; } = [];

        public DateOnly? LastDay { get; private set; }

        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoomDto>>([new RoomDto(1, "Sala Juárez", 8, "Piso 1")]);

        public Task<bool> RoomExistsAsync(int roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult(roomId == 1);

        public Task<IReadOnlyList<BookingRecord>> GetBookingsByDayAsync(
            DateOnly day, CancellationToken cancellationToken = default)
        {
            LastDay = day;
            return Task.FromResult<IReadOnlyList<BookingRecord>>(Bookings);
        }

        public Task<BookingRecord?> GetBookingAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Bookings.SingleOrDefault(b => b.Id == id));

        public Task<BookingRecord?> AddIfFreeAsync(NewBooking booking, CancellationToken cancellationToken = default)
        {
            if (SlotIsTaken)
            {
                return Task.FromResult<BookingRecord?>(null);
            }

            Added.Add(booking);
            return Task.FromResult<BookingRecord?>(new BookingRecord(
                Added.Count, booking.RoomId, booking.StartsAt, booking.EndsAt,
                booking.Subject, booking.UserId, booking.UserName));
        }

        public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            Deleted.Add(id);
            return Task.CompletedTask;
        }
    }

    private sealed record FakeCurrentUserService(
        bool IsAuthenticated,
        string? UserId,
        string? UserName,
        IReadOnlyList<string> Roles) : ICurrentUserService
    {
        public string? Email => null;

        public bool IsInRole(string role) => Roles.Contains(role);
    }
}

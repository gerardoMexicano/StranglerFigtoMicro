using Application.Common.Abstractions;
using Application.Features.Attendance.Abstractions;
using Application.Features.Attendance.Commands;
using Application.Features.Attendance.Dtos;
using Application.Features.Attendance.Queries;
using Domain.Common;

namespace UnitTests.Application;

public sealed class AttendanceHandlersTests
{
    private static readonly FakeCurrentUserService Juan = new(
        IsAuthenticated: true,
        UserId: "kc-user-001",
        UserName: "juan");

    [Fact]
    public async Task RegisterCheckIn_WithValidLocation_ShouldSaveItForTheTokenUser()
    {
        // Arrange
        var repository = new FakeCheckInRepository();
        var handler = new RegisterCheckInCommandHandler(repository, Juan);

        // Act
        var result = await handler.HandleAsync(new RegisterCheckInCommand(19.432608m, -99.133209m, 12.5m));

        // Assert
        Assert.True(result.IsSuccess);
        var saved = Assert.Single(repository.Added);
        Assert.Equal(new NewCheckIn("kc-user-001", "juan", 19.432608m, -99.133209m, 12.5m), saved);
        Assert.Equal(1, result.Value.Id);
    }

    [Fact]
    public async Task RegisterCheckIn_ShouldRoundToTheColumnPrecision()
    {
        // Arrange
        var repository = new FakeCheckInRepository();
        var handler = new RegisterCheckInCommandHandler(repository, Juan);

        // Act
        await handler.HandleAsync(new RegisterCheckInCommand(19.4326081234m, -99.1332099876m, 12.3456m));

        // Assert
        var saved = Assert.Single(repository.Added);
        Assert.Equal(19.432608m, saved.Latitude);
        Assert.Equal(-99.133210m, saved.Longitude);
        Assert.Equal(12.35m, saved.AccuracyMeters);
    }

    [Theory]
    [InlineData(null, -99.0, 10.0, "Attendance.InvalidLatitude")]
    [InlineData(90.1, -99.0, 10.0, "Attendance.InvalidLatitude")]
    [InlineData(-90.1, -99.0, 10.0, "Attendance.InvalidLatitude")]
    [InlineData(19.0, null, 10.0, "Attendance.InvalidLongitude")]
    [InlineData(19.0, 180.1, 10.0, "Attendance.InvalidLongitude")]
    [InlineData(19.0, -180.1, 10.0, "Attendance.InvalidLongitude")]
    [InlineData(19.0, -99.0, null, "Attendance.InvalidAccuracy")]
    [InlineData(19.0, -99.0, -1.0, "Attendance.InvalidAccuracy")]
    [InlineData(19.0, -99.0, 100000.1, "Attendance.InvalidAccuracy")]
    public async Task RegisterCheckIn_WithInvalidLocation_ShouldReturnValidationErrorAndNotSave(
        double? latitude, double? longitude, double? accuracy, string expectedCode)
    {
        // Arrange
        var repository = new FakeCheckInRepository();
        var handler = new RegisterCheckInCommandHandler(repository, Juan);
        var command = new RegisterCheckInCommand((decimal?)latitude, (decimal?)longitude, (decimal?)accuracy);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(repository.Added);
    }

    [Theory]
    [InlineData(90.0, 180.0, 0.0)]
    [InlineData(-90.0, -180.0, 100000.0)]
    public async Task RegisterCheckIn_OnTheLimits_ShouldBeValid(double latitude, double longitude, double accuracy)
    {
        // Arrange
        var handler = new RegisterCheckInCommandHandler(new FakeCheckInRepository(), Juan);

        // Act
        var result = await handler.HandleAsync(
            new RegisterCheckInCommand((decimal)latitude, (decimal)longitude, (decimal)accuracy));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RegisterCheckIn_WithoutUserId_ShouldReturnUnauthorizedAndNotSave()
    {
        // Arrange
        var repository = new FakeCheckInRepository();
        var handler = new RegisterCheckInCommandHandler(repository, Juan with { UserId = "" });

        // Act
        var result = await handler.HandleAsync(new RegisterCheckInCommand(19m, -99m, 10m));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task GetMyCheckIns_ShouldAskOnlyForTheTokenUser()
    {
        // Arrange
        var repository = new FakeCheckInRepository();
        var handler = new GetMyCheckInsQueryHandler(repository, Juan);

        // Act
        var result = await handler.HandleAsync(new GetMyCheckInsQuery(5));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(("kc-user-001", 5), repository.LastQuery);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GetMyCheckIns_WithTakeOutOfRange_ShouldReturnValidationError(int take)
    {
        // Arrange
        var repository = new FakeCheckInRepository();
        var handler = new GetMyCheckInsQueryHandler(repository, Juan);

        // Act
        var result = await handler.HandleAsync(new GetMyCheckInsQuery(take));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Null(repository.LastQuery);
    }

    [Fact]
    public async Task GetMyCheckIns_WithoutSession_ShouldReturnUnauthorized()
    {
        // Arrange
        var handler = new GetMyCheckInsQueryHandler(
            new FakeCheckInRepository(),
            new FakeCurrentUserService(IsAuthenticated: false, UserId: null, UserName: null));

        // Act
        var result = await handler.HandleAsync(new GetMyCheckInsQuery());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    private sealed class FakeCheckInRepository : ICheckInRepository
    {
        public List<NewCheckIn> Added { get; } = [];

        public (string UserId, int Take)? LastQuery { get; private set; }

        public Task<CheckInDto> AddAsync(NewCheckIn checkIn, CancellationToken cancellationToken = default)
        {
            Added.Add(checkIn);
            return Task.FromResult(new CheckInDto(
                Added.Count, checkIn.Latitude, checkIn.Longitude, checkIn.AccuracyMeters, DateTimeOffset.UnixEpoch));
        }

        public Task<IReadOnlyList<CheckInDto>> GetRecentByUserAsync(
            string userId, int take, CancellationToken cancellationToken = default)
        {
            LastQuery = (userId, take);
            return Task.FromResult<IReadOnlyList<CheckInDto>>([]);
        }
    }

    private sealed record FakeCurrentUserService(
        bool IsAuthenticated,
        string? UserId,
        string? UserName) : ICurrentUserService
    {
        public string? Email => null;

        public IReadOnlyList<string> Roles => [];

        public bool IsInRole(string role) => false;
    }
}

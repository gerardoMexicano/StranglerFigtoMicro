using Application.Common.Abstractions;
using Application.Common.Messaging;
using Application.Features.Attendance.Abstractions;
using Application.Features.Attendance.Dtos;
using Domain.Common;

namespace Application.Features.Attendance.Commands;

/// <summary>
/// La ubicación que da navigator.geolocation. Nullable: un campo que no llega es 400 y no (0, 0).
/// Quién registra no viaja en el cuerpo: sale del token.
/// </summary>
public sealed record RegisterCheckInCommand(
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters) : ICommand<CheckInDto>;

public sealed class RegisterCheckInCommandHandler(
    ICheckInRepository repository,
    ICurrentUserService currentUserService)
    : ICommandHandler<RegisterCheckInCommand, CheckInDto>
{
    // DECIMAL(9,6) en dbo.asistencias: ~0.1 m, de sobra para el GPS de un teléfono
    private const int CoordinateDecimals = 6;

    // Más que esto no ubica a nadie en un lugar (precisión por IP o por celda)
    public const decimal MaxAccuracyMeters = 100_000m;

    private readonly ICheckInRepository _repository = repository;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<Result<CheckInDto>> HandleAsync(
        RegisterCheckInCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Result.Failure<CheckInDto>(Error.Unauthorized(
                "Auth.Unauthenticated",
                "El usuario no tiene una sesión activa o el token de Keycloak es inválido."));
        }

        if (command.Latitude is not { } latitude || latitude is < -90m or > 90m)
        {
            return Result.Failure<CheckInDto>(Error.Validation(
                "Attendance.InvalidLatitude",
                "La latitud es obligatoria y debe estar entre -90 y 90."));
        }

        if (command.Longitude is not { } longitude || longitude is < -180m or > 180m)
        {
            return Result.Failure<CheckInDto>(Error.Validation(
                "Attendance.InvalidLongitude",
                "La longitud es obligatoria y debe estar entre -180 y 180."));
        }

        if (command.AccuracyMeters is not { } accuracy || accuracy < 0m || accuracy > MaxAccuracyMeters)
        {
            return Result.Failure<CheckInDto>(Error.Validation(
                "Attendance.InvalidAccuracy",
                $"La precisión es obligatoria y debe estar entre 0 y {MaxAccuracyMeters:0} metros."));
        }

        var checkIn = new NewCheckIn(
            UserId: _currentUserService.UserId,
            UserName: _currentUserService.UserName ?? string.Empty,
            Latitude: Math.Round(latitude, CoordinateDecimals),
            Longitude: Math.Round(longitude, CoordinateDecimals),
            AccuracyMeters: Math.Round(accuracy, 2));

        var saved = await _repository.AddAsync(checkIn, cancellationToken);
        return Result.Success(saved);
    }
}

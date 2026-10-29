namespace Application.Features.Attendance.Dtos;

/// <summary>Un check-in del Checador de Asistencia (antes /legacy/Usuarios.aspx).</summary>
public sealed record CheckInDto(
    long Id,
    decimal Latitude,
    decimal Longitude,
    decimal AccuracyMeters,
    DateTimeOffset RegisteredAt);

/// <summary>Lo que se guarda: la ubicación ya validada y quién la registró (claims del token).</summary>
public sealed record NewCheckIn(
    string UserId,
    string UserName,
    decimal Latitude,
    decimal Longitude,
    decimal AccuracyMeters);

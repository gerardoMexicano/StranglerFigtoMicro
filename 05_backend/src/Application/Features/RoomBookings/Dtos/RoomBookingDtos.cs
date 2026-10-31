namespace Application.Features.RoomBookings.Dtos;

/// <summary>Una sala que se puede reservar (dbo.salas).</summary>
public sealed record RoomDto(int Id, string Name, int Capacity, string Location);

/// <summary>
/// Una reservación como la ve quien pregunta. StartsAt/EndsAt son hora local de las salas
/// (sin zona horaria). IsMine y CanCancel los calcula el backend con el token.
/// </summary>
public sealed record BookingDto(
    long Id,
    int RoomId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Subject,
    string UserName,
    bool IsMine,
    bool CanCancel);

/// <summary>Una fila de dbo.reservaciones tal cual, con el dueño.</summary>
public sealed record BookingRecord(
    long Id,
    int RoomId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Subject,
    string UserId,
    string UserName);

/// <summary>Lo que se guarda: el horario ya validado y quién reserva (claims del token).</summary>
public sealed record NewBooking(
    int RoomId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Subject,
    string UserId,
    string UserName);

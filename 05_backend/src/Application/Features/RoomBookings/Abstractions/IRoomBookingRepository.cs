using Application.Features.RoomBookings.Dtos;

namespace Application.Features.RoomBookings.Abstractions;

public interface IRoomBookingRepository
{
    /// <summary>Las salas activas, por nombre.</summary>
    Task<IReadOnlyList<RoomDto>> GetRoomsAsync(CancellationToken cancellationToken = default);

    Task<bool> RoomExistsAsync(int roomId, CancellationToken cancellationToken = default);

    /// <summary>Las reservaciones que empiezan ese día, de todas las salas, por hora.</summary>
    Task<IReadOnlyList<BookingRecord>> GetBookingsByDayAsync(
        DateOnly day,
        CancellationToken cancellationToken = default);

    Task<BookingRecord?> GetBookingAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda la reservación solo si la sala está libre en ese horario, en una sola operación
    /// atómica. Devuelve null si se empalma con otra.
    /// </summary>
    Task<BookingRecord?> AddIfFreeAsync(NewBooking booking, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

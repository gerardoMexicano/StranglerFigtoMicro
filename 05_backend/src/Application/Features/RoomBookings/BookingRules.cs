using Application.Common.Abstractions;
using Application.Features.RoomBookings.Dtos;

namespace Application.Features.RoomBookings;

/// <summary>Horario de las salas y cómo se ve una reservación para quien pregunta.</summary>
public static class BookingRules
{
    public static readonly TimeOnly OpeningTime = new(8, 0);
    public static readonly TimeOnly ClosingTime = new(20, 0);
    public const int SlotMinutes = 30;
    public const int MaxSubjectLength = 120;

    public const string AdminRole = "admin";

    /// <summary>Cada quien cancela las suyas; admin cancela cualquiera.</summary>
    public static bool CanCancel(BookingRecord booking, ICurrentUserService user) =>
        booking.UserId == user.UserId || user.IsInRole(AdminRole);

    public static BookingDto ToDto(BookingRecord booking, ICurrentUserService user) => new(
        booking.Id,
        booking.RoomId,
        booking.StartsAt,
        booking.EndsAt,
        booking.Subject,
        booking.UserName,
        IsMine: booking.UserId == user.UserId,
        CanCancel: CanCancel(booking, user));
}

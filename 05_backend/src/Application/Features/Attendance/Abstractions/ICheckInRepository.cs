using Application.Features.Attendance.Dtos;

namespace Application.Features.Attendance.Abstractions;

public interface ICheckInRepository
{
    Task<CheckInDto> AddAsync(NewCheckIn checkIn, CancellationToken cancellationToken = default);

    /// <summary>Los últimos check-ins del usuario, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<CheckInDto>> GetRecentByUserAsync(
        string userId,
        int take,
        CancellationToken cancellationToken = default);
}

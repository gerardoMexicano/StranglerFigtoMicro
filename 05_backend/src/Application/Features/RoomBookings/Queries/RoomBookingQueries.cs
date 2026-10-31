using Application.Common.Abstractions;
using Application.Common.Messaging;
using Application.Features.RoomBookings.Abstractions;
using Application.Features.RoomBookings.Dtos;
using Domain.Common;

namespace Application.Features.RoomBookings.Queries;

public sealed record GetRoomsQuery : IQuery<IReadOnlyList<RoomDto>>;

public sealed class GetRoomsQueryHandler(IRoomBookingRepository repository)
    : IQueryHandler<GetRoomsQuery, IReadOnlyList<RoomDto>>
{
    private readonly IRoomBookingRepository _repository = repository;

    public async Task<Result<IReadOnlyList<RoomDto>>> HandleAsync(
        GetRoomsQuery query,
        CancellationToken cancellationToken = default) =>
        Result.Success(await _repository.GetRoomsAsync(cancellationToken));
}

/// <summary>La agenda de un día: las reservaciones de todas las salas, con quién reservó.</summary>
public sealed record GetBookingsByDayQuery(DateOnly Day) : IQuery<IReadOnlyList<BookingDto>>;

public sealed class GetBookingsByDayQueryHandler(
    IRoomBookingRepository repository,
    ICurrentUserService currentUserService)
    : IQueryHandler<GetBookingsByDayQuery, IReadOnlyList<BookingDto>>
{
    private readonly IRoomBookingRepository _repository = repository;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<Result<IReadOnlyList<BookingDto>>> HandleAsync(
        GetBookingsByDayQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Result.Failure<IReadOnlyList<BookingDto>>(Error.Unauthorized(
                "Auth.Unauthenticated",
                "El usuario no tiene una sesión activa o el token de Keycloak es inválido."));
        }

        var bookings = await _repository.GetBookingsByDayAsync(query.Day, cancellationToken);
        return Result.Success<IReadOnlyList<BookingDto>>(
            bookings.Select(b => BookingRules.ToDto(b, _currentUserService)).ToList());
    }
}

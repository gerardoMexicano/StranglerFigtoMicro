using Application.Common.Abstractions;
using Application.Common.Messaging;
using Application.Features.RoomBookings.Abstractions;
using Domain.Common;

namespace Application.Features.RoomBookings.Commands;

/// <summary>Cancelar una reservación: la propia, o cualquiera si eres admin.</summary>
public sealed record CancelBookingCommand(long Id) : ICommand;

public sealed class CancelBookingCommandHandler(
    IRoomBookingRepository repository,
    ICurrentUserService currentUserService)
    : ICommandHandler<CancelBookingCommand>
{
    private readonly IRoomBookingRepository _repository = repository;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<Result> HandleAsync(
        CancelBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Result.Failure(Error.Unauthorized(
                "Auth.Unauthenticated",
                "El usuario no tiene una sesión activa o el token de Keycloak es inválido."));
        }

        var booking = await _repository.GetBookingAsync(command.Id, cancellationToken);
        if (booking is null)
        {
            return Result.Failure(Error.NotFound(
                "Rooms.BookingNotFound",
                $"No existe la reservación {command.Id}."));
        }

        if (!BookingRules.CanCancel(booking, _currentUserService))
        {
            return Result.Failure(Error.Forbidden(
                "Rooms.NotYourBooking",
                "Solo puedes cancelar tus propias reservaciones."));
        }

        await _repository.DeleteAsync(command.Id, cancellationToken);
        return Result.Success();
    }
}

using Application.Common.Abstractions;
using Application.Common.Messaging;
using Application.Features.RoomBookings.Abstractions;
using Application.Features.RoomBookings.Dtos;
using Domain.Common;

namespace Application.Features.RoomBookings.Commands;

/// <summary>
/// Reservar una sala. Las horas van en hora local de las salas y sin zona horaria
/// ("2026-10-30T09:00:00"). Quién reserva no viaja en el cuerpo: sale del token.
/// </summary>
public sealed record CreateBookingCommand(
    int? RoomId,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? Subject) : ICommand<BookingDto>;

public sealed class CreateBookingCommandHandler(
    IRoomBookingRepository repository,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider)
    : ICommandHandler<CreateBookingCommand, BookingDto>
{
    private readonly IRoomBookingRepository _repository = repository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<Result<BookingDto>> HandleAsync(
        CreateBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Result.Failure<BookingDto>(Error.Unauthorized(
                "Auth.Unauthenticated",
                "El usuario no tiene una sesión activa o el token de Keycloak es inválido."));
        }

        if (command.RoomId is not { } roomId)
        {
            return Result.Failure<BookingDto>(Error.Validation(
                "Rooms.RoomRequired",
                "Elige la sala que quieres reservar."));
        }

        var subject = command.Subject?.Trim() ?? string.Empty;
        if (subject.Length is 0 or > BookingRules.MaxSubjectLength)
        {
            return Result.Failure<BookingDto>(Error.Validation(
                "Rooms.InvalidSubject",
                $"El asunto es obligatorio y puede tener hasta {BookingRules.MaxSubjectLength} caracteres."));
        }

        var scheduleError = ValidateSchedule(command.StartsAt, command.EndsAt);
        if (scheduleError is not null)
        {
            return Result.Failure<BookingDto>(scheduleError);
        }

        var startsAt = command.StartsAt!.Value;
        var endsAt = command.EndsAt!.Value;

        if (!await _repository.RoomExistsAsync(roomId, cancellationToken))
        {
            return Result.Failure<BookingDto>(Error.NotFound(
                "Rooms.RoomNotFound",
                $"No existe la sala {roomId}."));
        }

        var saved = await _repository.AddIfFreeAsync(
            new NewBooking(
                roomId,
                startsAt,
                endsAt,
                subject,
                _currentUserService.UserId,
                _currentUserService.UserName ?? string.Empty),
            cancellationToken);

        return saved is null
            ? Result.Failure<BookingDto>(Error.Conflict(
                "Rooms.Overlap",
                $"La sala ya está reservada en parte de ese horario ({startsAt:HH:mm}–{endsAt:HH:mm})."))
            : Result.Success(BookingRules.ToDto(saved, _currentUserService));
    }

    private Error? ValidateSchedule(DateTime? startsAtValue, DateTime? endsAtValue)
    {
        if (startsAtValue is not { } startsAt || endsAtValue is not { } endsAt)
        {
            return Error.Validation("Rooms.ScheduleRequired", "Indica la hora de inicio y la de fin.");
        }

        // Con zona horaria ("Z" o "-06:00") System.Text.Json la convierte y la hora cambia sin avisar
        if (startsAt.Kind != DateTimeKind.Unspecified || endsAt.Kind != DateTimeKind.Unspecified)
        {
            return Error.Validation(
                "Rooms.ScheduleWithOffset",
                "Las horas van en hora local de las salas, sin zona horaria (p. ej. 2026-10-30T09:00:00).");
        }

        if (!IsOnSlot(startsAt) || !IsOnSlot(endsAt))
        {
            return Error.Validation(
                "Rooms.ScheduleNotOnSlot",
                $"Las reservaciones van en bloques de {BookingRules.SlotMinutes} minutos (09:00, 09:30, ...).");
        }

        if (endsAt <= startsAt)
        {
            return Error.Validation("Rooms.ScheduleInverted", "La hora de fin debe ser posterior a la de inicio.");
        }

        if (startsAt.Date != endsAt.Date
            || TimeOnly.FromDateTime(startsAt) < BookingRules.OpeningTime
            || TimeOnly.FromDateTime(endsAt) > BookingRules.ClosingTime)
        {
            return Error.Validation(
                "Rooms.ScheduleOutOfHours",
                $"Las salas se reservan el mismo día entre {BookingRules.OpeningTime:HH:mm} y {BookingRules.ClosingTime:HH:mm}.");
        }

        if (startsAt <= _timeProvider.GetLocalNow().DateTime)
        {
            return Error.Validation("Rooms.ScheduleInThePast", "No se puede reservar un horario que ya pasó.");
        }

        return null;
    }

    private static bool IsOnSlot(DateTime value) =>
        value.Minute % BookingRules.SlotMinutes == 0 && value.Second == 0 && value.Millisecond == 0;
}

using Api.Extensions;
using Application.Common.Messaging;
using Application.Features.RoomBookings.Commands;
using Application.Features.RoomBookings.Dtos;
using Application.Features.RoomBookings.Queries;

namespace Api.Endpoints;

/// <summary>Reservación de salas: funcionalidad nueva que se ve en /legacy/AngularPage.</summary>
public sealed class RoomBookingEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms")
            .WithTags("Room bookings (AngularPage)");

        group.MapGet("/", async (
            IQueryHandler<GetRoomsQuery, IReadOnlyList<RoomDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetRoomsQuery(), cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .RequireAuthorization("Lectura")
        .WithName("GetRooms")
        .WithSummary("Salas activas que se pueden reservar.")
        .Produces<IReadOnlyList<RoomDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/bookings", async (
            DateOnly date,
            IQueryHandler<GetBookingsByDayQuery, IReadOnlyList<BookingDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetBookingsByDayQuery(date), cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .RequireAuthorization("Lectura")
        .WithName("GetBookingsByDay")
        .WithSummary("Reservaciones de todas las salas en un día (?date=2026-10-30).")
        .Produces<IReadOnlyList<BookingDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // invitado es solo consulta: ve la agenda pero no reserva ni cancela
        group.MapPost("/bookings", async (
            CreateBookingCommand command,
            ICommandHandler<CreateBookingCommand, BookingDto> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.IsSuccess
                ? TypedResults.Created((string?)null, result.Value)
                : result.ToProblemDetails();
        })
        .RequireAuthorization("UsuarioOrAdmin")
        .WithName("CreateBooking")
        .WithSummary("Reserva una sala a nombre del usuario autenticado.")
        .Produces<BookingDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/bookings/{id:long}", async (
            long id,
            ICommandHandler<CancelBookingCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new CancelBookingCommand(id), cancellationToken);
            return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
        })
        .RequireAuthorization("UsuarioOrAdmin")
        .WithName("CancelBooking")
        .WithSummary("Cancela una reservación propia; admin puede cancelar cualquiera.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

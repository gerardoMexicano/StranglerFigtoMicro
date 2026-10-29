using Api.Extensions;
using Application.Common.Messaging;
using Application.Features.Attendance.Commands;
using Application.Features.Attendance.Dtos;
using Application.Features.Attendance.Queries;

namespace Api.Endpoints;

/// <summary>Checador de Asistencia: lo que hacía /legacy/Usuarios.aspx, ahora con registro.</summary>
public sealed class AttendanceEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance/check-ins")
            .WithTags("Attendance (Usuarios)");

        // invitado es solo consulta: puede ver la pantalla pero no registrar asistencia
        group.MapPost("/", async (
            RegisterCheckInCommand command,
            ICommandHandler<RegisterCheckInCommand, CheckInDto> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.IsSuccess
                ? TypedResults.Created((string?)null, result.Value)
                : result.ToProblemDetails();
        })
        .RequireAuthorization("UsuarioOrAdmin")
        .WithName("RegisterCheckIn")
        .WithSummary("Registra la ubicación actual del usuario autenticado como asistencia.")
        .Produces<CheckInDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/mine", async (
            int? take,
            IQueryHandler<GetMyCheckInsQuery, IReadOnlyList<CheckInDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetMyCheckInsQuery(take ?? 10), cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .RequireAuthorization("Lectura")
        .WithName("GetMyCheckIns")
        .WithSummary("Últimos check-ins del usuario autenticado, del más reciente al más antiguo.")
        .Produces<IReadOnlyList<CheckInDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}

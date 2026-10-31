using Application.Common.Abstractions;
using Application.Common.Messaging;
using Application.Features.Attendance.Abstractions;
using Application.Features.Attendance.Dtos;
using Domain.Common;

namespace Application.Features.Attendance.Queries;

/// <summary>Los últimos check-ins de quien hace la petición; nunca los de otro usuario.</summary>
public sealed record GetMyCheckInsQuery(int Take = 10) : IQuery<IReadOnlyList<CheckInDto>>;

public sealed class GetMyCheckInsQueryHandler(
    ICheckInRepository repository,
    ICurrentUserService currentUserService)
    : IQueryHandler<GetMyCheckInsQuery, IReadOnlyList<CheckInDto>>
{
    public const int MaxTake = 50;

    private readonly ICheckInRepository _repository = repository;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<Result<IReadOnlyList<CheckInDto>>> HandleAsync(
        GetMyCheckInsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Result.Failure<IReadOnlyList<CheckInDto>>(Error.Unauthorized(
                "Auth.Unauthenticated",
                "El usuario no tiene una sesión activa o el token de Keycloak es inválido."));
        }

        if (query.Take is < 1 or > MaxTake)
        {
            return Result.Failure<IReadOnlyList<CheckInDto>>(Error.Validation(
                "Attendance.InvalidTake",
                $"Se pueden pedir entre 1 y {MaxTake} registros."));
        }

        var checkIns = await _repository.GetRecentByUserAsync(
            _currentUserService.UserId, query.Take, cancellationToken);

        return Result.Success(checkIns);
    }
}

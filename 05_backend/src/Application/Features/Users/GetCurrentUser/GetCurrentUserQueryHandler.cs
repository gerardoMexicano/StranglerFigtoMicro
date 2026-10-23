using Application.Common.Abstractions;
using Application.Common.Messaging;
using Domain.Common;

namespace Application.Features.Users.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(ICurrentUserService currentUserService)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public Task<Result<CurrentUserResponse>> HandleAsync(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            var error = Error.Unauthorized(
                "Auth.Unauthenticated",
                "El usuario no tiene una sesión activa o el token de Keycloak es inválido.");

            return Task.FromResult(Result.Failure<CurrentUserResponse>(error));
        }

        var response = new CurrentUserResponse(
            UserId: _currentUserService.UserId,
            UserName: _currentUserService.UserName ?? string.Empty,
            Email: _currentUserService.Email,
            Roles: _currentUserService.Roles);

        return Task.FromResult(Result.Success(response));
    }
}

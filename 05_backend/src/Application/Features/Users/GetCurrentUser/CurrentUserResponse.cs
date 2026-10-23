namespace Application.Features.Users.GetCurrentUser;

public sealed record CurrentUserResponse(
    string UserId,
    string UserName,
    string? Email,
    IReadOnlyList<string> Roles);

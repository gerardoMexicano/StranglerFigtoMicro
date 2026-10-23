namespace Application.Common.Abstractions;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? UserName { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }

    bool IsInRole(string role);
}

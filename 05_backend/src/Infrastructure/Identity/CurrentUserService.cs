using System.Security.Claims;
using Application.Common.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Identity;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        Principal?.FindFirstValue("sub") ??
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        Principal?.FindFirstValue("preferred_username") ??
        Principal?.FindFirstValue(ClaimTypes.Name);

    public string? Email =>
        Principal?.FindFirstValue("email") ??
        Principal?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyList<string> Roles =>
        Principal is null
            ? []
            : Principal.FindAll(ClaimTypes.Role)
                .Concat(Principal.FindAll("role"))
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    public bool IsInRole(string role) =>
        Principal?.IsInRole(role) == true ||
        Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

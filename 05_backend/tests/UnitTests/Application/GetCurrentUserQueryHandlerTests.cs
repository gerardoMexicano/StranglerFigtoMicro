using Application.Common.Abstractions;
using Application.Features.Users.GetCurrentUser;
using Domain.Common;

namespace UnitTests.Application;

public sealed class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenUserIsAuthenticated_ShouldReturnCurrentUserResponse()
    {
        // Arrange
        var currentUser = new FakeCurrentUserService(
            IsAuthenticated: true,
            UserId: "f8d9a1bc-2345-6789-abcd-ef0123456789",
            UserName: "juan",
            Email: "juan@laboratorio.local",
            Roles: ["usuario", "admin"]);

        var handler = new GetCurrentUserQueryHandler(currentUser);
        var query = new GetCurrentUserQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("f8d9a1bc-2345-6789-abcd-ef0123456789", result.Value.UserId);
        Assert.Equal("juan", result.Value.UserName);
        Assert.Equal("juan@laboratorio.local", result.Value.Email);
        Assert.Contains("admin", result.Value.Roles);
        Assert.Contains("usuario", result.Value.Roles);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotAuthenticated_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var currentUser = new FakeCurrentUserService(
            IsAuthenticated: false,
            UserId: null,
            UserName: null,
            Email: null,
            Roles: []);

        var handler = new GetCurrentUserQueryHandler(currentUser);
        var query = new GetCurrentUserQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIdIsMissing_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var currentUser = new FakeCurrentUserService(
            IsAuthenticated: true,
            UserId: "",
            UserName: "juan",
            Email: null,
            Roles: ["invitado"]);

        var handler = new GetCurrentUserQueryHandler(currentUser);
        var query = new GetCurrentUserQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    private sealed record FakeCurrentUserService(
        bool IsAuthenticated,
        string? UserId,
        string? UserName,
        string? Email,
        IReadOnlyList<string> Roles) : ICurrentUserService
    {
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}

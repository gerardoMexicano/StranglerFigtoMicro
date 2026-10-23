using System.Security.Claims;
using Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace UnitTests.Infrastructure;

public sealed class KeycloakClaimsTransformationTests
{
    private readonly KeycloakClaimsTransformation _sut;

    public KeycloakClaimsTransformationTests()
    {
        var options = Options.Create(new OidcOptions
        {
            Authority = "http://localhost/auth/realms/laboratorio",
            Audience = "backend-api"
        });
        _sut = new KeycloakClaimsTransformation(options);
    }

    [Fact]
    public async Task TransformAsync_WithKeycloakRealmRolesJson_ShouldMapRolesToStandardRoleClaims()
    {
        // Arrange
        const string realmAccessJson = """{"roles":["admin","usuario"]}""";
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "user-123"),
            new Claim("preferred_username", "juan"),
            new Claim("realm_access", realmAccessJson)
        ], authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var transformed = await _sut.TransformAsync(principal);

        // Assert
        Assert.True(transformed.IsInRole("admin"));
        Assert.True(transformed.IsInRole("usuario"));
        Assert.False(transformed.IsInRole("invitado"));
    }

    [Fact]
    public async Task TransformAsync_WithKeycloakResourceAccessJson_ShouldMapAudienceClientRoles()
    {
        // Arrange
        const string resourceAccessJson = """{"backend-api":{"roles":["reportes:read","reportes:export"]}}""";
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "user-456"),
            new Claim("resource_access", resourceAccessJson)
        ], authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var transformed = await _sut.TransformAsync(principal);

        // Assert
        Assert.True(transformed.IsInRole("reportes:read"));
        Assert.True(transformed.IsInRole("reportes:export"));
    }

    [Fact]
    public async Task TransformAsync_WhenCalledMultipleTimes_ShouldNotDuplicateRoleClaims()
    {
        // Arrange
        const string realmAccessJson = """{"roles":["admin"]}""";
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "user-123"),
            new Claim("realm_access", realmAccessJson)
        ], authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var firstPass = await _sut.TransformAsync(principal);
        var secondPass = await _sut.TransformAsync(firstPass);

        // Assert
        var adminClaims = secondPass.FindAll(ClaimTypes.Role).Where(c => c.Value == "admin").ToList();
        Assert.Single(adminClaims);
    }

    [Fact]
    public async Task TransformAsync_WhenPrincipalIsNotAuthenticated_ShouldLeaveClaimsUnchanged()
    {
        // Arrange
        const string realmAccessJson = """{"roles":["admin"]}""";
        var unauthenticatedIdentity = new ClaimsIdentity([new Claim("realm_access", realmAccessJson)]);
        var principal = new ClaimsPrincipal(unauthenticatedIdentity);

        // Act
        var transformed = await _sut.TransformAsync(principal);

        // Assert
        Assert.False(transformed.IsInRole("admin"));
    }
}

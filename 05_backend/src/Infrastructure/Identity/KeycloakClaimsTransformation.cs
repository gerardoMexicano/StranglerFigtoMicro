using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity;

public sealed class KeycloakClaimsTransformation(IOptions<OidcOptions> oidcOptions) : IClaimsTransformation
{
    private readonly OidcOptions _oidcOptions = oidcOptions.Value;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
        {
            return Task.FromResult(principal);
        }

        // 1. Extraer roles del realm desde el claim JSON "realm_access": {"roles":["admin","usuario"]}
        var realmAccessClaim = identity.FindFirst("realm_access");
        if (realmAccessClaim is not null && !string.IsNullOrWhiteSpace(realmAccessClaim.Value))
        {
            AddRolesFromElement(identity, realmAccessClaim.Value, clientId: null);
        }

        // 2. Extraer roles específicos del cliente/audience desde "resource_access": {"backend-api":{"roles":[...]}}
        var resourceAccessClaim = identity.FindFirst("resource_access");
        if (resourceAccessClaim is not null &&
            !string.IsNullOrWhiteSpace(resourceAccessClaim.Value) &&
            !string.IsNullOrWhiteSpace(_oidcOptions.Audience))
        {
            AddRolesFromElement(identity, resourceAccessClaim.Value, _oidcOptions.Audience);
        }

        return Task.FromResult(principal);
    }

    private static void AddRolesFromElement(ClaimsIdentity identity, string json, string? clientId)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var targetElement = document.RootElement;

            if (clientId is not null)
            {
                if (!targetElement.TryGetProperty(clientId, out targetElement))
                {
                    return;
                }
            }

            if (!targetElement.TryGetProperty("roles", out var rolesElement) ||
                rolesElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var roleElement in rolesElement.EnumerateArray())
            {
                var role = roleElement.GetString();
                if (string.IsNullOrWhiteSpace(role))
                {
                    continue;
                }

                if (!identity.HasClaim(identity.RoleClaimType, role))
                {
                    identity.AddClaim(new Claim(identity.RoleClaimType, role));
                }
            }
        }
        catch (JsonException)
        {
            // Si el claim no es JSON válido, se ignora sin interrumpir la autenticación.
        }
    }
}

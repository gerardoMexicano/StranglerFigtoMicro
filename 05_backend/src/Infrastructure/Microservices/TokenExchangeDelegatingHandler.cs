using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Infrastructure.Microservices;

/// <summary>
/// DelegatingHandler preparado para OAuth 2.0 Token Exchange (RFC 8693) contra Keycloak.
/// Cuando este backend necesite invocar a otro microservicio interno, intercambia el
/// access_token entrante del usuario por un token emitido específicamente para el
/// audience (targetClientId) del microservicio destino.
/// </summary>
public sealed class TokenExchangeDelegatingHandler(
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    IOptions<TokenExchangeOptions> options,
    string targetAudience) : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly TokenExchangeOptions _options = options.Value;
    private readonly string _targetAudience = targetAudience;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null && !string.IsNullOrWhiteSpace(_options.TokenEndpoint))
        {
            var subjectToken = await httpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrWhiteSpace(subjectToken))
            {
                var exchangedToken = await ExchangeTokenAsync(subjectToken, cancellationToken);
                if (!string.IsNullOrWhiteSpace(exchangedToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", exchangedToken);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> ExchangeTokenAsync(string subjectToken, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("KeycloakTokenExchange");
        using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["subject_token"] = subjectToken,
            ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
            ["requested_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
            ["audience"] = _targetAudience
        });

        using var response = await client.PostAsync(_options.TokenEndpoint, formContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenExchangeResponse>(cancellationToken: cancellationToken);
        return tokenResponse?.AccessToken;
    }

    private sealed record TokenExchangeResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);
}

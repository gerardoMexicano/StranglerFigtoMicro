namespace Infrastructure.Microservices;

/// <summary>
/// Configuración preparada para OAuth 2.0 Token Exchange (RFC 8693) contra Keycloak
/// cuando este backend requiera invocar a otros microservicios internos.
/// </summary>
public sealed class TokenExchangeOptions
{
    public const string SectionName = "TokenExchange";

    public string TokenEndpoint { get; init; } = string.Empty;

    public string ClientId { get; init; } = "backend-api";

    public string ClientSecret { get; init; } = string.Empty;
}

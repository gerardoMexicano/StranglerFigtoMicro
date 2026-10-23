using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Identity;

public sealed class OidcOptions
{
    public const string SectionName = "Oidc";

    [Required(AllowEmptyStrings = false)]
    public string Authority { get; init; } = string.Empty;

    public string? MetadataAddress { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = "backend-api";

    public bool RequireHttpsMetadata { get; init; }
}

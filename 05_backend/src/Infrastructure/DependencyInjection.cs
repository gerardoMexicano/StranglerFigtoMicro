using Application.Common.Abstractions;
using Application.Features.Sales.Abstractions;
using Infrastructure.Identity;
using Infrastructure.Microservices;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Persistencia con Dapper (DefaultConnection y WideWorldImporters dedicada)
        services.AddSingleton<IDbConnectionFactory>(_ =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'ConnectionStrings:DefaultConnection'.");
            return new SqlConnectionFactory(connectionString);
        });

        services.AddSingleton<IWwiDbConnectionFactory>(_ =>
        {
            var wwiConnectionString = configuration.GetConnectionString("WideWorldImporters")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'ConnectionStrings:WideWorldImporters'.");
            return new WwiSqlConnectionFactory(wwiConnectionString);
        });

        services.AddScoped<ISalesReadRepository, SalesReadRepository>();

        // 2. Configuración OIDC de Keycloak (contrato Oidc__* compartido con 07_reverse/01_deployment)
        services.AddOptions<OidcOptions>()
            .Bind(configuration.GetSection(OidcOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var oidcOptions = configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>() ?? new OidcOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = oidcOptions.Authority;
                if (!string.IsNullOrWhiteSpace(oidcOptions.MetadataAddress))
                {
                    options.MetadataAddress = oidcOptions.MetadataAddress;
                }

                options.Audience = oidcOptions.Audience;
                options.RequireHttpsMetadata = oidcOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = oidcOptions.Authority,
                    ValidateAudience = true,
                    ValidAudience = oidcOptions.Audience,
                    ValidateLifetime = true,
                    NameClaimType = "preferred_username",
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("admin"))
            .AddPolicy("UsuarioOrAdmin", policy => policy.RequireRole("usuario", "admin"));

        services.AddHttpContextAccessor();
        services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformation>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // 3. Opciones preparadas para Token Exchange (RFC 8693) hacia futuros microservicios
        services.Configure<TokenExchangeOptions>(
            configuration.GetSection(TokenExchangeOptions.SectionName));
        services.AddHttpClient("KeycloakTokenExchange");

        return services;
    }
}

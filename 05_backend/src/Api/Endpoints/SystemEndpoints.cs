using Application.Common.Messaging;
using Application.Features.Users.GetCurrentUser;
using Api.Extensions;

namespace Api.Endpoints;

public sealed class SystemEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system")
            .WithTags("System");

        group.MapGet("/me", async (
            IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetCurrentUserQuery(), cancellationToken);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .RequireAuthorization()
        .WithName("GetCurrentUser")
        .WithSummary("Obtiene el perfil e identidad del usuario autenticado desde el token de Keycloak.")
        .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}

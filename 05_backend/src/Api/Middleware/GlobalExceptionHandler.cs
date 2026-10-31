using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Falta un parámetro obligatorio (?date=) o el JSON no se puede leer: es culpa de la
        // petición, no del servidor. Minimal APIs lo lanza como BadHttpRequestException (400).
        if (exception is BadHttpRequestException badRequest)
        {
            _logger.LogInformation("Solicitud inválida: {Message}", badRequest.Message);

            httpContext.Response.StatusCode = badRequest.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = "Solicitud inválida",
                Detail = badRequest.Message,
                Instance = httpContext.Request.Path
            }, cancellationToken);

            return true;
        }

        _logger.LogError(exception, "Excepción no controlada: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Error interno del servidor",
            Detail = "Ocurrió un error inesperado al procesar la solicitud.",
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

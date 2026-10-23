using Domain.Common;

namespace Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("No se puede convertir un resultado exitoso en ProblemDetails.");
        }

        var statusCode = result.Error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var title = result.Error.Type switch
        {
            ErrorType.Validation => "Solicitud inválida",
            ErrorType.Unauthorized => "No autorizado",
            ErrorType.Forbidden => "Acceso denegado",
            ErrorType.NotFound => "Recurso no encontrado",
            ErrorType.Conflict => "Conflicto de estado",
            _ => "Error interno del servidor"
        };

        return TypedResults.Problem(
            title: title,
            detail: result.Error.Description,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = result.Error.Code
            });
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Filters;

/// <summary>
/// Convierte errores conocidos en respuestas HTTP comprensibles.
/// Los errores inesperados se registran en el servidor y nunca exponen
/// detalles internos, SQL, rutas o trazas al cliente.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
        => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;

        var (status, title) = exception switch
        {
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Credenciales no válidas"),

            SecurityValidationException or RequestValidationException
                => (StatusCodes.Status400BadRequest, "Datos no válidos"),

            SecurityNotFoundException or ResourceNotFoundException or OrderNotFoundException or KeyNotFoundException
                => (StatusCodes.Status404NotFound, "No encontrado"),

            DuplicateEmailException or ConflictException or InvalidOrderStateException
                => (StatusCodes.Status409Conflict, "Conflicto"),

            SecurityRuleException or BusinessRuleException or EmptyOrderException or InsufficientStockException
                => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio incumplida"),

            _ => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Error inesperado procesando {Path}", context.HttpContext.Request.Path);
        }

        var detail = status == StatusCodes.Status500InternalServerError
            ? "Ocurrió un error interno. Inténtalo nuevamente o contacta al administrador."
            : exception.Message;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        })
        { StatusCode = status };

        context.ExceptionHandled = true;
    }
}

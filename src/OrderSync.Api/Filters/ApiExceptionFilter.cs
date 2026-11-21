using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Filters;

/// <summary>Convierte las excepciones de negocio en respuestas HTTP uniformes (ProblemDetails).</summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, title) = context.Exception switch
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

            _ => (0, "")
        };

        if (status == 0) return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = context.Exception.Message
        })
        { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

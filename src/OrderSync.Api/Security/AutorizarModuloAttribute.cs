using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Security;

/// <summary>
/// Filtro de autorización por módulo (HU-06). Consulta la matriz seguridad.Permiso:
/// 401 si no hay sesión, 403 si el rol no alcanza el nivel (o el alcance) exigido.
/// Ejemplos:
///   [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "pago")]
///   [AutorizarModulo("RRHH", PermissionLevel.Lectura, AlcanceNoContiene = "asistencia")]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AutorizarModuloAttribute : Attribute, IAsyncAuthorizationFilter
{
    public AutorizarModuloAttribute(string modulo, int nivelMinimo)
    {
        Modulo = modulo;
        NivelMinimo = nivelMinimo;
    }

    public string Modulo { get; }
    public int NivelMinimo { get; }

    /// <summary>Texto que debe aparecer en el alcance del permiso cuando el acceso es parcial.</summary>
    public string? AlcanceContiene { get; set; }

    /// <summary>Texto que NO debe aparecer en el alcance del permiso cuando el acceso es parcial.</summary>
    public string? AlcanceNoContiene { get; set; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var usuarioId = context.HttpContext.User.GetUserId();
        if (usuarioId is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var service = context.HttpContext.RequestServices.GetRequiredService<ModuleAuthorizationService>();
        var decision = await service.CheckAsync(
            usuarioId.Value, Modulo, NivelMinimo, AlcanceContiene,
            context.HttpContext.RequestAborted, AlcanceNoContiene);

        if (!decision.Allowed)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Acceso denegado",
                Detail = decision.Reason
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Nomina;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Supervisor calcula el pago (RRHH, acceso parcial con alcance "pago"). Cajero y Cocina reciben 403.</summary>
[ApiController]
[Route("api/nomina")]
[AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "pago")]
public sealed class NominaController : ControllerBase
{
    private readonly PayrollService _service;

    public NominaController(PayrollService service) => _service = service;

    /// <summary>Calcula y guarda la nómina de un mes (de un empleado o de todos).</summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(PayrollRunResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PayrollRunResult>> Calculate(
        [FromBody] CalculatePayrollRequest body, CancellationToken ct)
    {
        try
        {
            var result = await _service.CalculateAsync(
                new PayrollRequest(body.Anio, body.Mes, body.EmpleadoId, User.GetUserId()), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Parámetros de nómina incompletos", detail: ex.Message, statusCode: 422);
        }
    }
}

public sealed class CalculatePayrollRequest
{
    [Range(2000, 2100)] public int Anio { get; init; }
    [Range(1, 12)] public int Mes { get; init; }
    public int? EmpleadoId { get; init; }
}

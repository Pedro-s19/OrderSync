using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Rrhh;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>
/// Empleados. Los ven Supervisor y Gerencia (Cajero y Cocina, con acceso solo a "su asistencia", quedan fuera);
/// el Supervisor (alcance "turnos y pago") los gestiona.
/// </summary>
[ApiController]
[Route("api/empleados")]
[AutorizarModulo("RRHH", PermissionLevel.Lectura, AlcanceNoContiene = "asistencia")]
public sealed class EmpleadosController : ControllerBase
{
    private readonly EmployeeService _employees;

    public EmpleadosController(EmployeeService employees) => _employees = employees;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> List(
        [FromQuery] bool incluirInactivos = false, CancellationToken ct = default)
        => Ok(await _employees.ListAsync(incluirInactivos, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Get(int id, CancellationToken ct)
        => Ok(await _employees.GetAsync(id, ct));

    [HttpPost]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] EmployeeRequest request, CancellationToken ct)
    {
        var created = await _employees.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.EmpleadoId }, created);
    }

    [HttpPut("{id:int}")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, [FromBody] EmployeeRequest request, CancellationToken ct)
        => Ok(await _employees.UpdateAsync(id, request, ct));

    /// <summary>Desactiva al empleado: deja de entrar en la nómina (CP-05).</summary>
    [HttpPost("{id:int}/desactivar")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<EmployeeDto>> Deactivate(int id, CancellationToken ct)
        => Ok(await _employees.SetActiveAsync(id, false, ct));

    [HttpPost("{id:int}/activar")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<EmployeeDto>> Activate(int id, CancellationToken ct)
        => Ok(await _employees.SetActiveAsync(id, true, ct));
}

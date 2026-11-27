using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Rrhh;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Vincula la cuenta de un usuario a su ficha de empleado. Solo el administrador.</summary>
[ApiController]
[Route("api/usuarios/{id:int}/empleado")]
[AutorizarModulo("USUARIOS", PermissionLevel.Total)]
public sealed class UsuarioEmpleadoController : ControllerBase
{
    private readonly EmployeeService _employees;

    public UsuarioEmpleadoController(EmployeeService employees) => _employees = employees;

    /// <summary>Con {"empleadoId": 3} vincula; con {"empleadoId": null} desvincula.</summary>
    [HttpPut]
    public async Task<IActionResult> Link(int id, [FromBody] LinkEmployeeRequest request, CancellationToken ct)
    {
        await _employees.LinkUserAsync(id, request.EmpleadoId, ct);
        return NoContent();
    }
}

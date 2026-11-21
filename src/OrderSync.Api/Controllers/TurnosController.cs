using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Rrhh;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Turnos de trabajo (FR-13). Los programa el Supervisor; Gerencia los consulta.</summary>
[ApiController]
[Route("api/turnos")]
[AutorizarModulo("RRHH", PermissionLevel.Lectura, AlcanceNoContiene = "asistencia")]
public sealed class TurnosController : ControllerBase
{
    private readonly ShiftService _shifts;

    public TurnosController(ShiftService shifts) => _shifts = shifts;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShiftDto>>> List(
        [FromQuery] int? empleadoId, [FromQuery] string? estado,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 50, CancellationToken ct = default)
        => Ok(await _shifts.ListAsync(new ShiftFilter(empleadoId, estado, desde, hasta, pagina, tamano), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShiftDto>> Get(int id, CancellationToken ct)
        => Ok(await _shifts.GetAsync(id, ct));

    /// <summary>Programa un turno. Se rechaza si se cruza con otro turno del mismo empleado.</summary>
    [HttpPost]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<ShiftDto>> Create([FromBody] ShiftRequest request, CancellationToken ct)
    {
        var created = await _shifts.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.TurnoId }, created);
    }

    [HttpPut("{id:int}")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<ShiftDto>> Update(int id, [FromBody] ShiftTimesRequest request, CancellationToken ct)
        => Ok(await _shifts.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/cancelar")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<ShiftDto>> Cancel(int id, CancellationToken ct)
        => Ok(await _shifts.CancelAsync(id, ct));
}

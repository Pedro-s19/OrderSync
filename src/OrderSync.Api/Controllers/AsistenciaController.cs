using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Rrhh;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>
/// Asistencia (FR-14). Cajero y Cocina marcan solo la suya ("mia", exige un usuario vinculado a un empleado);
/// el Supervisor marca por otros y corrige; Gerencia consulta.
/// </summary>
[ApiController]
[Route("api/asistencia")]
[AutorizarModulo("RRHH", PermissionLevel.Lectura)]
public sealed class AsistenciaController : ControllerBase
{
    private readonly AttendanceService _attendance;

    public AsistenciaController(AttendanceService attendance) => _attendance = attendance;

    // ---------- Supervisor y Gerencia ----------

    [HttpGet]
    [AutorizarModulo("RRHH", PermissionLevel.Lectura, AlcanceNoContiene = "asistencia")]
    public async Task<ActionResult<IReadOnlyList<AttendanceDto>>> List(
        [FromQuery] int? empleadoId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] bool soloAbiertas = false, [FromQuery] int pagina = 1, [FromQuery] int tamano = 50,
        CancellationToken ct = default)
        => Ok(await _attendance.ListAsync(new AttendanceFilter(empleadoId, desde, hasta, soloAbiertas, pagina, tamano), ct));

    [HttpPost("entrada")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<AttendanceDto>> ClockIn([FromBody] ClockRequest request, CancellationToken ct)
        => Ok(await _attendance.ClockInAsync(request.EmpleadoId, ct));

    [HttpPost("salida")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<AttendanceDto>> ClockOut([FromBody] ClockRequest request, CancellationToken ct)
        => Ok(await _attendance.ClockOutAsync(request.EmpleadoId, ct));

    /// <summary>Corrige una asistencia (por ejemplo, para cerrar un turno sin salida antes de liquidar).</summary>
    [HttpPut("{id:int}")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "turnos")]
    public async Task<ActionResult<AttendanceDto>> Correct(
        int id, [FromBody] CorrectAttendanceRequest request, CancellationToken ct)
        => Ok(await _attendance.CorrectAsync(id, request, ct));

    // ---------- Cajero y Cocina: solo su asistencia ----------

    [HttpGet("mia")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "asistencia")]
    public async Task<ActionResult<IReadOnlyList<AttendanceDto>>> ListMine(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
        => Ok(await _attendance.ListMineAsync(User.RequireUserId(), desde, hasta, ct));

    [HttpPost("mia/entrada")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "asistencia")]
    public async Task<ActionResult<AttendanceDto>> ClockInMine(CancellationToken ct)
        => Ok(await _attendance.ClockInMineAsync(User.RequireUserId(), ct));

    [HttpPost("mia/salida")]
    [AutorizarModulo("RRHH", PermissionLevel.Parcial, AlcanceContiene = "asistencia")]
    public async Task<ActionResult<AttendanceDto>> ClockOutMine(CancellationToken ct)
        => Ok(await _attendance.ClockOutMineAsync(User.RequireUserId(), ct));
}

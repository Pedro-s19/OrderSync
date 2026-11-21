using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Usuarios, roles y bitácora. Solo el administrador (módulo USUARIOS, nivel total).</summary>
[ApiController]
[Route("api")]
[AutorizarModulo("USUARIOS", PermissionLevel.Total)]
public sealed class UsuariosController : ControllerBase
{
    private readonly UserAdministrationService _service;

    public UsuariosController(UserAdministrationService service) => _service = service;

    [HttpGet("usuarios")]
    public async Task<ActionResult<IReadOnlyList<UserSummary>>> List(CancellationToken ct)
        => Ok(await _service.ListUsersAsync(ct));

    [HttpPost("usuarios")]
    [ProducesResponseType(typeof(UserSummary), StatusCodes.Status201Created)]
    public async Task<ActionResult<UserSummary>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var created = await _service.CreateUserAsync(User.RequireUserId(), request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("usuarios/{id:int}/rol")]
    public async Task<ActionResult<UserSummary>> AssignRole(
        int id, [FromBody] AssignRoleRequest request, CancellationToken ct)
        => Ok(await _service.AssignRoleAsync(User.RequireUserId(), id, request.RolId, ct));

    [HttpGet("usuarios/bitacora")]
    public async Task<ActionResult<IReadOnlyList<RoleAuditEntry>>> Audit(
        [FromQuery] int? usuarioId, CancellationToken ct)
        => Ok(await _service.ListAuditAsync(usuarioId, ct));

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleSummary>>> Roles(CancellationToken ct)
        => Ok(await _service.ListRolesAsync(ct));
}

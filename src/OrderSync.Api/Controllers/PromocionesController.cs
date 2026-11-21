using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Crm;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Promociones (FR-17). Las gestiona Gerencia (alcance "promociones"); el resto de CRM las consulta.</summary>
[ApiController]
[Route("api/promociones")]
[AutorizarModulo("CRM", PermissionLevel.Lectura)]
public sealed class PromocionesController : ControllerBase
{
    private readonly PromotionService _promotions;

    public PromocionesController(PromotionService promotions) => _promotions = promotions;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> List(
        [FromQuery] bool incluirInactivas = false, CancellationToken ct = default)
        => Ok(await _promotions.ListAsync(incluirInactivas, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PromotionDto>> Get(int id, CancellationToken ct)
        => Ok(await _promotions.GetAsync(id, ct));

    [HttpPost]
    [AutorizarModulo("CRM", PermissionLevel.Parcial, AlcanceContiene = "promociones")]
    public async Task<ActionResult<PromotionDto>> Create([FromBody] PromotionRequest request, CancellationToken ct)
    {
        var created = await _promotions.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.PromocionId }, created);
    }

    [HttpPut("{id:int}")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial, AlcanceContiene = "promociones")]
    public async Task<ActionResult<PromotionDto>> Update(int id, [FromBody] PromotionRequest request, CancellationToken ct)
        => Ok(await _promotions.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/desactivar")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial, AlcanceContiene = "promociones")]
    public async Task<ActionResult<PromotionDto>> Deactivate(int id, CancellationToken ct)
        => Ok(await _promotions.SetActiveAsync(id, false, ct));

    [HttpPost("{id:int}/activar")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial, AlcanceContiene = "promociones")]
    public async Task<ActionResult<PromotionDto>> Activate(int id, CancellationToken ct)
        => Ok(await _promotions.SetActiveAsync(id, true, ct));
}

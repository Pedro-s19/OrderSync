using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Crm;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Clientes, historial, puntos y promociones asignadas. Cajero y Gerencia gestionan; Supervisor consulta.</summary>
[ApiController]
[Route("api/clientes")]
[AutorizarModulo("CRM", PermissionLevel.Lectura)]
public sealed class ClientesController : ControllerBase
{
    private readonly CustomerService _customers;
    private readonly PromotionService _promotions;

    public ClientesController(CustomerService customers, PromotionService promotions)
    {
        _customers = customers;
        _promotions = promotions;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> List(
        [FromQuery] string? texto, [FromQuery] bool incluirInactivos = false,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 50, CancellationToken ct = default)
        => Ok(await _customers.ListAsync(new CustomerFilter(texto, incluirInactivos, pagina, tamano), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Get(int id, CancellationToken ct)
        => Ok(await _customers.GetAsync(id, ct));

    /// <summary>Perfil con los últimos pedidos y movimientos de puntos (FR-18).</summary>
    [HttpGet("{id:int}/historial")]
    public async Task<ActionResult<CustomerHistory>> History(int id, CancellationToken ct)
        => Ok(await _customers.GetHistoryAsync(id, ct));

    [HttpPost]
    [AutorizarModulo("CRM", PermissionLevel.Parcial)]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CustomerRequest request, CancellationToken ct)
    {
        var created = await _customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.ClienteId }, created);
    }

    [HttpPut("{id:int}")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial)]
    public async Task<ActionResult<CustomerDto>> Update(int id, [FromBody] CustomerRequest request, CancellationToken ct)
        => Ok(await _customers.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/desactivar")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial)]
    public async Task<ActionResult<CustomerDto>> Deactivate(int id, CancellationToken ct)
        => Ok(await _customers.SetActiveAsync(id, false, ct));

    [HttpPost("{id:int}/activar")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial)]
    public async Task<ActionResult<CustomerDto>> Activate(int id, CancellationToken ct)
        => Ok(await _customers.SetActiveAsync(id, true, ct));

    /// <summary>Suma o resta puntos a mano (negativo para canjear). El saldo no puede quedar negativo.</summary>
    [HttpPost("{id:int}/puntos")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial, AlcanceContiene = "puntos")]
    public async Task<ActionResult<CustomerDto>> AdjustPoints(int id, [FromBody] PointsRequest request, CancellationToken ct)
        => Ok(await _customers.AdjustPointsAsync(id, request, ct));

    // ---------- Promociones del cliente (Gerencia) ----------

    [HttpGet("{id:int}/promociones")]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> ListPromotions(int id, CancellationToken ct)
        => Ok(await _promotions.ListForCustomerAsync(id, ct));

    [HttpPut("{id:int}/promociones/{promocionId:int}")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial, AlcanceContiene = "promociones")]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> AssignPromotion(
        int id, int promocionId, CancellationToken ct)
        => Ok(await _promotions.AssignAsync(id, promocionId, ct));

    [HttpDelete("{id:int}/promociones/{promocionId:int}")]
    [AutorizarModulo("CRM", PermissionLevel.Parcial, AlcanceContiene = "promociones")]
    public async Task<IActionResult> UnassignPromotion(int id, int promocionId, CancellationToken ct)
    {
        await _promotions.UnassignAsync(id, promocionId, ct);
        return NoContent();
    }
}

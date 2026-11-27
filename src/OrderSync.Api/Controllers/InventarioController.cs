using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Inventario;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>
/// Matriz (Tabla 10): el Supervisor atiende alertas y registra mermas y ajustes;
/// el Administrador del Sistema tiene acceso total; Gerencia solo lectura; Cajero y Cocina no entran.
/// </summary>
[ApiController]
[Route("api/inventario")]
[AutorizarModulo("INVENTARIO", PermissionLevel.Lectura)]
public sealed class InventarioController : ControllerBase
{
    private readonly IStockAlertReader _alerts;
    private readonly IngredientService _ingredients;
    private readonly StockMovementService _movements;

    public InventarioController(
        IStockAlertReader alerts, IngredientService ingredients, StockMovementService movements)
    {
        _alerts = alerts;
        _ingredients = ingredients;
        _movements = movements;
    }

    // ---------- Alertas ----------

    /// <summary>Alertas de stock crítico. Por defecto solo las ABIERTAS; use ?estado=ATENDIDA o ?estado=todas.</summary>
    [HttpGet("alertas")]
    [ProducesResponseType(typeof(IReadOnlyList<StockAlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<StockAlertDto>>> GetAlertas(
        [FromQuery] string? estado, CancellationToken ct)
    {
        string? filtro;
        if (string.IsNullOrWhiteSpace(estado))
            filtro = AlertStates.Abierta;
        else if (estado.Equals("todas", StringComparison.OrdinalIgnoreCase))
            filtro = null;
        else if (estado.Equals(AlertStates.Abierta, StringComparison.OrdinalIgnoreCase))
            filtro = AlertStates.Abierta;
        else if (estado.Equals(AlertStates.Atendida, StringComparison.OrdinalIgnoreCase))
            filtro = AlertStates.Atendida;
        else
            return Problem(title: "Estado no válido", detail: "Use ABIERTA, ATENDIDA o todas.", statusCode: 400);

        return Ok(await _alerts.GetAsync(filtro, ct));
    }

    /// <summary>Marca una alerta como atendida (ya se reabasteció o se revisó).</summary>
    [HttpPost("alertas/{id:int}/atender")]
    [AutorizarModulo("INVENTARIO", PermissionLevel.Parcial, AlcanceContiene = "alertas")]
    public async Task<IActionResult> AtenderAlerta(int id, CancellationToken ct)
    {
        await _movements.AttendAlertAsync(id, ct);
        return NoContent();
    }

    // ---------- Ingredientes ----------

    [HttpGet("ingredientes")]
    public async Task<ActionResult<IReadOnlyList<IngredientDto>>> ListIngredients(
        [FromQuery] bool incluirInactivos = false, CancellationToken ct = default)
        => Ok(await _ingredients.ListAsync(incluirInactivos, ct));

    [HttpGet("ingredientes/{id:int}")]
    public async Task<ActionResult<IngredientDto>> GetIngredient(int id, CancellationToken ct)
        => Ok(await _ingredients.GetAsync(id, ct));

    [HttpPost("ingredientes")]
    [AutorizarModulo("INVENTARIO", PermissionLevel.Total)]
    public async Task<ActionResult<IngredientDto>> CreateIngredient(
        [FromBody] CreateIngredientRequest request, CancellationToken ct)
    {
        var created = await _ingredients.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetIngredient), new { id = created.IngredienteId }, created);
    }

    /// <summary>Edita nombre, unidad y mínimo. El stock solo cambia con mermas, ajustes, ventas o recepciones.</summary>
    [HttpPut("ingredientes/{id:int}")]
    [AutorizarModulo("INVENTARIO", PermissionLevel.Total)]
    public async Task<ActionResult<IngredientDto>> UpdateIngredient(
        int id, [FromBody] UpdateIngredientRequest request, CancellationToken ct)
        => Ok(await _ingredients.UpdateAsync(id, request, ct));

    [HttpPost("ingredientes/{id:int}/desactivar")]
    [AutorizarModulo("INVENTARIO", PermissionLevel.Total)]
    public async Task<ActionResult<IngredientDto>> DeactivateIngredient(int id, CancellationToken ct)
        => Ok(await _ingredients.SetActiveAsync(id, false, ct));

    [HttpPost("ingredientes/{id:int}/activar")]
    [AutorizarModulo("INVENTARIO", PermissionLevel.Total)]
    public async Task<ActionResult<IngredientDto>> ActivateIngredient(int id, CancellationToken ct)
        => Ok(await _ingredients.SetActiveAsync(id, true, ct));

    // ---------- Mermas, ajustes y movimientos ----------

    /// <summary>Registra una merma (FR-22): descuenta stock con motivo obligatorio.</summary>
    [HttpPost("mermas")]
    [AutorizarModulo("INVENTARIO", PermissionLevel.Parcial, AlcanceContiene = "mermas")]
    public async Task<ActionResult<StockChangeResult>> RegisterWaste([FromBody] WasteRequest request, CancellationToken ct)
        => Ok(await _movements.RegisterWasteAsync(User.GetUserId(), request, ct));

    /// <summary>Ajusta el stock al valor real contado físicamente.</summary>
    [HttpPost("ajustes")]
    [AutorizarModulo("INVENTARIO", PermissionLevel.Parcial, AlcanceContiene = "ajustes")]
    public async Task<ActionResult<StockChangeResult>> RegisterAdjustment(
        [FromBody] AdjustmentRequest request, CancellationToken ct)
        => Ok(await _movements.RegisterAdjustmentAsync(User.GetUserId(), request, ct));

    /// <summary>Historial de movimientos. Filtros: ingredienteId, tipo, desde, hasta, pagina y tamano.</summary>
    [HttpGet("movimientos")]
    public async Task<ActionResult<IReadOnlyList<MovementDto>>> ListMovements(
        [FromQuery] int? ingredienteId, [FromQuery] string? tipo,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 50, CancellationToken ct = default)
        => Ok(await _movements.ListMovementsAsync(
            new MovementFilter(ingredienteId, tipo, desde, hasta, pagina, tamano), ct));
}

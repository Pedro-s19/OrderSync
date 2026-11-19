using Microsoft.AspNetCore.Mvc;
using OrderSync.Application.Inventario;

namespace OrderSync.Api.Controllers;

[ApiController]
[Route("api/inventario")]
public sealed class InventarioController : ControllerBase
{
    private readonly IStockAlertReader _alerts;

    public InventarioController(IStockAlertReader alerts) => _alerts = alerts;

    /// <summary>Alertas de stock crítico. Por defecto solo las ABIERTAS; use ?estado=ATENDIDA o ?estado=todas.</summary>
    [HttpGet("alertas")]
    [ProducesResponseType(typeof(IReadOnlyList<StockAlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            return Problem(title: "Estado no válido",
                detail: "Use ABIERTA, ATENDIDA o todas.", statusCode: 400);

        return Ok(await _alerts.GetAsync(filtro, ct));
    }
}
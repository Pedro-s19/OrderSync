using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Facturacion;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Cobro y facturas (FR-10, FR-26). Cobra el Cajero; la lista de facturas la ven Supervisor y Gerencia.</summary>
[ApiController]
[Route("api/facturacion")]
[AutorizarModulo("FACTURACION", PermissionLevel.Lectura)]
public sealed class FacturacionController : ControllerBase
{
    private readonly BillingService _billing;

    public FacturacionController(BillingService billing) => _billing = billing;

    /// <summary>
    /// Cobra un pedido: aplica la promoción que corresponda, guarda factura y pagos, y suma puntos al cliente.
    /// Los pagos deben sumar exactamente el total (subtotal - descuento + propina).
    /// </summary>
    [HttpPost("cobros")]
    [AutorizarModulo("FACTURACION", PermissionLevel.Parcial, AlcanceContiene = "cobro")]
    [ProducesResponseType(typeof(ChargeResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChargeResult>> Charge([FromBody] ChargeRequest request, CancellationToken ct)
    {
        var result = await _billing.ChargeAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Factura.FacturaId }, result);
    }

    [HttpGet("facturas")]
    [AutorizarModulo("FACTURACION", PermissionLevel.Lectura, AlcanceNoContiene = "cobro")]
    public async Task<ActionResult<IReadOnlyList<InvoiceSummary>>> List(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 50, CancellationToken ct = default)
        => Ok(await _billing.ListAsync(new InvoiceFilter(desde, hasta, pagina, tamano), ct));

    [HttpGet("facturas/{id:int}")]
    public async Task<ActionResult<InvoiceDto>> Get(int id, CancellationToken ct)
        => Ok(await _billing.GetAsync(id, ct));

    [HttpGet("pedidos/{pedidoId:int}/factura")]
    public async Task<ActionResult<InvoiceDto>> GetByOrder(int pedidoId, CancellationToken ct)
        => Ok(await _billing.GetByOrderAsync(pedidoId, ct));
}

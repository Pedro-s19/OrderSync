using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Proveedores;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Órdenes de compra y recepción de mercancía (FR-08, FR-09). Gestiona el Administrador del Sistema.</summary>
[ApiController]
[Route("api/ordenes-compra")]
[AutorizarModulo("PROVEEDORES", PermissionLevel.Lectura)]
public sealed class OrdenesCompraController : ControllerBase
{
    private readonly PurchaseOrderService _orders;

    public OrdenesCompraController(PurchaseOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderSummary>>> List(
        [FromQuery] string? estado, [FromQuery] int? proveedorId,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 50, CancellationToken ct = default)
        => Ok(await _orders.ListAsync(new PurchaseOrderFilter(estado, proveedorId, pagina, tamano), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PurchaseOrderDetail>> Get(int id, CancellationToken ct)
        => Ok(await _orders.GetAsync(id, ct));

    /// <summary>Crea la orden en BORRADOR. Si no se indica precioUnitario, se usa el de la lista del proveedor.</summary>
    [HttpPost]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<PurchaseOrderDetail>> Create([FromBody] PurchaseOrderRequest request, CancellationToken ct)
    {
        var created = await _orders.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.OrdenCompraId }, created);
    }

    [HttpPut("{id:int}")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<PurchaseOrderDetail>> Update(
        int id, [FromBody] PurchaseOrderRequest request, CancellationToken ct)
        => Ok(await _orders.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/enviar")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<PurchaseOrderDetail>> Send(int id, CancellationToken ct)
        => Ok(await _orders.SendAsync(id, ct));

    [HttpPost("{id:int}/cancelar")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<PurchaseOrderDetail>> Cancel(int id, CancellationToken ct)
        => Ok(await _orders.CancelAsync(id, ct));

    /// <summary>Registra mercancía recibida (total o parcial): suma el stock de inmediato.</summary>
    [HttpPost("{id:int}/recepciones")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<ReceiptResult>> Receive(int id, [FromBody] ReceiptRequest request, CancellationToken ct)
        => Ok(await _orders.ReceiveAsync(User.GetUserId(), id, request, ct));
}

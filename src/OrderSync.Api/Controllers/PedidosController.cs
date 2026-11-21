using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Inventario;
using OrderSync.Application.Pedidos;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>
/// Matriz (Tabla 10): Cajero total; Cocina ve y marca listo; Supervisor consulta y cancela;
/// Gerencia y Administrador solo lectura.
/// </summary>
[ApiController]
[Route("api/pedidos")]
[AutorizarModulo("PEDIDOS", PermissionLevel.Lectura)]
public sealed class PedidosController : ControllerBase
{
    private readonly OrderService _orders;
    private readonly OrderConfirmationService _confirmation;

    public PedidosController(OrderService orders, OrderConfirmationService confirmation)
    {
        _orders = orders;
        _confirmation = confirmation;
    }

    /// <summary>Lista pedidos. Filtros: estado, canal, desde, hasta (fecha), pagina y tamano.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderSummary>>> List(
        [FromQuery] string? estado, [FromQuery] string? canal,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 50, CancellationToken ct = default)
        => Ok(await _orders.ListAsync(new OrderFilter(estado, canal, desde, hasta, pagina, tamano), ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetail>> Get(int id, CancellationToken ct)
        => Ok(await _orders.GetAsync(id, ct));

    /// <summary>Crea un pedido en BORRADOR. Los precios salen del menú, no del cuerpo de la petición.</summary>
    [HttpPost]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Total)]
    [ProducesResponseType(typeof(OrderDetail), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderDetail>> Create([FromBody] OrderRequest request, CancellationToken ct)
    {
        var created = await _orders.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.PedidoId }, created);
    }

    /// <summary>Edita un pedido mientras esté en BORRADOR.</summary>
    [HttpPut("{id:int}")]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Total)]
    public async Task<ActionResult<OrderDetail>> Update(int id, [FromBody] OrderRequest request, CancellationToken ct)
        => Ok(await _orders.UpdateAsync(id, request, ct));

    /// <summary>Confirma el pedido: descuenta los ingredientes por receta y genera alertas de stock.</summary>
    [HttpPost("{id:int}/confirmar")]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Total)]
    [ProducesResponseType(typeof(OrderConfirmationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderConfirmationResult>> Confirmar(int id, CancellationToken ct)
        => Ok(await _confirmation.ConfirmAsync(id, User.GetUserId(), ct));

    /// <summary>Cocina marca el pedido como listo (FR-05).</summary>
    [HttpPost("{id:int}/listo")]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Parcial, AlcanceContiene = "marcar listo")]
    public async Task<ActionResult<OrderDetail>> MarcarListo(int id, CancellationToken ct)
        => Ok(await _orders.MarkReadyAsync(id, ct));

    [HttpPost("{id:int}/entregar")]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Total)]
    public async Task<ActionResult<OrderDetail>> Entregar(int id, CancellationToken ct)
        => Ok(await _orders.DeliverAsync(id, ct));

    /// <summary>Cancela un pedido (BORRADOR o CONFIRMADO). Si estaba confirmado, devuelve los ingredientes al inventario.</summary>
    [HttpPost("{id:int}/cancelar")]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Parcial, AlcanceContiene = "cancelar")]
    public async Task<ActionResult<OrderCancellationResult>> Cancelar(int id, CancellationToken ct)
        => Ok(await _orders.CancelAsync(id, User.GetUserId(), ct));
}

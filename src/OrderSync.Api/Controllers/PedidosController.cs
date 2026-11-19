using Microsoft.AspNetCore.Mvc;
using OrderSync.Application.Inventario;

namespace OrderSync.Api.Controllers;

[ApiController]
[Route("api/pedidos")]
public sealed class PedidosController : ControllerBase
{
    private readonly OrderConfirmationService _confirmation;

    public PedidosController(OrderConfirmationService confirmation) => _confirmation = confirmation;

    /// <summary>Confirma el pedido: descuenta los ingredientes por receta y genera alertas de stock.</summary>
    [HttpPost("{id:int}/confirmar")]
    [ProducesResponseType(typeof(OrderConfirmationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderConfirmationResult>> Confirmar(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _confirmation.ConfirmAsync(id, null, ct));
        }
        catch (OrderNotFoundException ex)
        {
            return Problem(title: "Pedido no encontrado", detail: ex.Message, statusCode: 404);
        }
        catch (InvalidOrderStateException ex)
        {
            return Problem(title: "Estado del pedido no válido", detail: ex.Message, statusCode: 409);
        }
        catch (EmptyOrderException ex)
        {
            return Problem(title: "Pedido sin productos", detail: ex.Message, statusCode: 422);
        }
        catch (InsufficientStockException ex)
        {
            return Problem(title: "Stock insuficiente", detail: ex.Message, statusCode: 422);
        }
    }
}
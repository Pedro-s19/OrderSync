using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Pedidos;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Menú de productos. Lo ven todos los roles con acceso a Pedidos; solo el cajero (acceso total) lo modifica.</summary>
[ApiController]
[Route("api/productos")]
[AutorizarModulo("PEDIDOS", PermissionLevel.Lectura)]
public sealed class ProductosController : ControllerBase
{
    private readonly ProductService _products;

    public ProductosController(ProductService products) => _products = products;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductInfo>>> List(
        [FromQuery] bool incluirInactivos = false, CancellationToken ct = default)
        => Ok(await _products.ListAsync(incluirInactivos, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductInfo>> Get(int id, CancellationToken ct)
        => Ok(await _products.GetAsync(id, ct));

    [HttpPost]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Total)]
    public async Task<ActionResult<ProductInfo>> Create([FromBody] ProductRequest request, CancellationToken ct)
    {
        var created = await _products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.ProductoId }, created);
    }

    [HttpPut("{id:int}")]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Total)]
    public async Task<ActionResult<ProductInfo>> Update(int id, [FromBody] ProductRequest request, CancellationToken ct)
        => Ok(await _products.UpdateAsync(id, request, ct));

    /// <summary>Desactiva el producto (no se borra): deja de poder venderse.</summary>
    [HttpPost("{id:int}/desactivar")]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Total)]
    public async Task<ActionResult<ProductInfo>> Deactivate(int id, CancellationToken ct)
        => Ok(await _products.SetActiveAsync(id, false, ct));

    [HttpPost("{id:int}/activar")]
    [AutorizarModulo("PEDIDOS", PermissionLevel.Total)]
    public async Task<ActionResult<ProductInfo>> Activate(int id, CancellationToken ct)
        => Ok(await _products.SetActiveAsync(id, true, ct));
}

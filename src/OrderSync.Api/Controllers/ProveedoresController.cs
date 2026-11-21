using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Proveedores;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Directorio, lista de precios y comparación. Supervisor y Gerencia consultan; el Administrador gestiona.</summary>
[ApiController]
[Route("api/proveedores")]
[AutorizarModulo("PROVEEDORES", PermissionLevel.Lectura)]
public sealed class ProveedoresController : ControllerBase
{
    private readonly SupplierService _suppliers;

    public ProveedoresController(SupplierService suppliers) => _suppliers = suppliers;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> List(
        [FromQuery] bool incluirInactivos = false, CancellationToken ct = default)
        => Ok(await _suppliers.ListAsync(incluirInactivos, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierDto>> Get(int id, CancellationToken ct)
        => Ok(await _suppliers.GetAsync(id, ct));

    [HttpPost]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] SupplierRequest request, CancellationToken ct)
    {
        var created = await _suppliers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.ProveedorId }, created);
    }

    [HttpPut("{id:int}")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<SupplierDto>> Update(int id, [FromBody] SupplierRequest request, CancellationToken ct)
        => Ok(await _suppliers.UpdateAsync(id, request, ct));

    /// <summary>Desactiva al proveedor (FR-23): deja de aparecer en comparaciones y órdenes nuevas.</summary>
    [HttpPost("{id:int}/desactivar")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<SupplierDto>> Deactivate(int id, CancellationToken ct)
        => Ok(await _suppliers.SetActiveAsync(id, false, ct));

    [HttpPost("{id:int}/activar")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<SupplierDto>> Activate(int id, CancellationToken ct)
        => Ok(await _suppliers.SetActiveAsync(id, true, ct));

    // ---------- Lista de precios ----------

    [HttpGet("{id:int}/precios")]
    public async Task<ActionResult<IReadOnlyList<SupplierPriceDto>>> GetPrices(int id, CancellationToken ct)
        => Ok(await _suppliers.GetPricesAsync(id, ct));

    /// <summary>Crea o actualiza el precio que ofrece el proveedor por un ingrediente.</summary>
    [HttpPut("{id:int}/precios/{ingredienteId:int}")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<ActionResult<IReadOnlyList<SupplierPriceDto>>> SetPrice(
        int id, int ingredienteId, [FromBody] PriceRequest request, CancellationToken ct)
        => Ok(await _suppliers.SetPriceAsync(id, ingredienteId, request, ct));

    [HttpDelete("{id:int}/precios/{ingredienteId:int}")]
    [AutorizarModulo("PROVEEDORES", PermissionLevel.Total)]
    public async Task<IActionResult> RemovePrice(int id, int ingredienteId, CancellationToken ct)
    {
        await _suppliers.RemovePriceAsync(id, ingredienteId, ct);
        return NoContent();
    }

    /// <summary>Compara el precio de un ingrediente entre proveedores activos, del más barato al más caro (FR-20).</summary>
    [HttpGet("comparar-precios")]
    public async Task<ActionResult<IReadOnlyList<PriceComparisonDto>>> Compare(
        [FromQuery] int ingredienteId, CancellationToken ct)
        => Ok(await _suppliers.ComparePricesAsync(ingredienteId, ct));
}

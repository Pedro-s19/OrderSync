using Microsoft.AspNetCore.Mvc;
using OrderSync.Api.Security;
using OrderSync.Application.Facturacion;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

/// <summary>Reportes de ventas (FR-11, FR-12). Los ven Gerencia y Supervisor; el Cajero queda fuera (NFR-06).</summary>
[ApiController]
[Route("api/facturacion/reportes")]
[AutorizarModulo("FACTURACION", PermissionLevel.Lectura, AlcanceNoContiene = "cobro")]
public sealed class ReportesController : ControllerBase
{
    private readonly ReportService _reports;

    public ReportesController(ReportService reports) => _reports = reports;

    /// <summary>Ventas agrupadas por día en un rango de fechas locales (inclusive).</summary>
    [HttpGet("ventas-por-dia")]
    public async Task<ActionResult<DailySalesReport>> DailySales(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta, CancellationToken ct)
        => Ok(await _reports.DailySalesAsync(desde, hasta, ct));

    /// <summary>Productos más vendidos, ordenados por cantidad o por ingresos.</summary>
    [HttpGet("productos-mas-vendidos")]
    public async Task<ActionResult<IReadOnlyList<ProductSalesDto>>> TopProducts(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta,
        [FromQuery] string? orden, [FromQuery] int top = 10, CancellationToken ct = default)
        => Ok(await _reports.TopProductsAsync(desde, hasta, orden, top, ct));
}

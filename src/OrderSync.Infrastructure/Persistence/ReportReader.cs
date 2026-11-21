using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Facturacion;

namespace OrderSync.Infrastructure.Persistence;

public sealed class ReportReader : IReportReader
{
    private readonly OrderSyncDbContext _db;

    public ReportReader(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<InvoiceRow>> GetInvoiceRowsAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken ct = default)
    {
        var filas = await _db.Facturas.AsNoTracking()
            .Where(f => f.Fecha >= desdeUtc && f.Fecha < hastaUtcExclusivo)
            .Select(f => new { f.Fecha, f.Subtotal, f.Propina, f.Total })
            .ToListAsync(ct);

        return filas.Select(f => new InvoiceRow(f.Fecha, f.Subtotal, f.Propina, f.Total)).ToList();
    }

    public async Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken ct = default)
    {
        var filas = await _db.DetallePedidos.AsNoTracking()
            .Where(d => d.Pedido.Factura != null
                        && d.Pedido.Factura.Fecha >= desdeUtc
                        && d.Pedido.Factura.Fecha < hastaUtcExclusivo)
            .GroupBy(d => new { d.ProductoId, d.Producto.Nombre })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.Nombre,
                Cantidad = g.Sum(x => x.Cantidad),
                Ingresos = g.Sum(x => x.Cantidad * x.PrecioUnitario)
            })
            .ToListAsync(ct);

        return filas.Select(f => new ProductSalesRow(f.ProductoId, f.Nombre, f.Cantidad, f.Ingresos)).ToList();
    }
}

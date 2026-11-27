using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Facturacion;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class InvoiceStore : IInvoiceStore
{
    private readonly OrderSyncDbContext _db;

    public InvoiceStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<bool> ExistsForOrderAsync(int pedidoId, CancellationToken ct = default)
        => _db.Facturas.AnyAsync(f => f.PedidoId == pedidoId, ct);

    public async Task<InvoiceDto> CreateAsync(NewInvoiceData invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var ahora = DateTime.UtcNow;
        var entity = new Factura
        {
            PedidoId = invoice.PedidoId,
            Fecha = ahora,
            Subtotal = invoice.Subtotal,
            Propina = invoice.Propina,
            Total = invoice.Total
        };
        foreach (var p in invoice.Pagos)
            entity.Pagos.Add(new Pago { Metodo = p.Metodo, Monto = p.Monto, Fecha = ahora });

        _db.Facturas.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetAsync(entity.FacturaId, ct))!;
    }

    public Task<InvoiceDto?> GetAsync(int facturaId, CancellationToken ct = default)
        => LoadAsync(f => f.FacturaId == facturaId, ct);

    public Task<InvoiceDto?> GetByOrderAsync(int pedidoId, CancellationToken ct = default)
        => LoadAsync(f => f.PedidoId == pedidoId, ct);

    public async Task<IReadOnlyList<InvoiceSummary>> ListAsync(InvoiceQuery query, CancellationToken ct = default)
    {
        var q = _db.Facturas.AsNoTracking().AsQueryable();
        if (query.DesdeUtc is DateTime desde) q = q.Where(f => f.Fecha >= desde);
        if (query.HastaUtcExclusivo is DateTime hasta) q = q.Where(f => f.Fecha < hasta);

        return await q
            .OrderByDescending(f => f.Fecha)
            .Skip((query.Pagina - 1) * query.TamanoPagina)
            .Take(query.TamanoPagina)
            .Select(f => new InvoiceSummary(f.FacturaId, f.PedidoId, f.Fecha, f.Total))
            .ToListAsync(ct);
    }

    private async Task<InvoiceDto?> LoadAsync(
        System.Linq.Expressions.Expression<Func<Factura, bool>> filter, CancellationToken ct)
    {
        var f = await _db.Facturas.AsNoTracking()
            .Where(filter)
            .Select(x => new { x.FacturaId, x.PedidoId, x.Fecha, x.Subtotal, x.Propina, x.Total })
            .FirstOrDefaultAsync(ct);
        if (f is null) return null;

        var pagos = (await _db.Pagos.AsNoTracking()
                .Where(p => p.FacturaId == f.FacturaId)
                .OrderBy(p => p.PagoId)
                .Select(p => new { p.PagoId, p.Metodo, p.Monto, p.Fecha })
                .ToListAsync(ct))
            .Select(p => new PaymentDto(p.PagoId, p.Metodo, p.Monto, p.Fecha))
            .ToList();

        return new InvoiceDto(f.FacturaId, f.PedidoId, f.Fecha, f.Subtotal,
            f.Subtotal + f.Propina - f.Total, f.Propina, f.Total, pagos);
    }
}

using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Proveedores;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly OrderSyncDbContext _db;

    public PurchaseOrderRepository(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<PurchaseOrderSummary>> ListAsync(PurchaseOrderFilter filter, CancellationToken ct = default)
    {
        var query = _db.OrdenCompras.AsNoTracking().AsQueryable();
        if (filter.Estado is not null) query = query.Where(o => o.Estado == filter.Estado);
        if (filter.ProveedorId is not null) query = query.Where(o => o.ProveedorId == filter.ProveedorId);

        return await query
            .OrderByDescending(o => o.FechaCreacion)
            .Skip((filter.Pagina - 1) * filter.TamanoPagina)
            .Take(filter.TamanoPagina)
            .Select(o => new PurchaseOrderSummary(
                o.OrdenCompraId, o.ProveedorId, o.Proveedor.Nombre, o.Estado, o.FechaCreacion, o.FechaEsperada,
                o.DetalleOrdenCompras.Sum(d => d.Cantidad * d.PrecioUnitario)))
            .ToListAsync(ct);
    }

    public async Task<PurchaseOrderDetail?> GetAsync(int ordenCompraId, CancellationToken ct = default)
    {
        var o = await _db.OrdenCompras.AsNoTracking()
            .Where(x => x.OrdenCompraId == ordenCompraId)
            .Select(x => new
            {
                x.OrdenCompraId, x.ProveedorId, Proveedor = x.Proveedor.Nombre, x.UsuarioId,
                x.Estado, x.FechaCreacion, x.FechaEsperada
            })
            .FirstOrDefaultAsync(ct);
        if (o is null) return null;

        var lineas = (await _db.DetalleOrdenCompras.AsNoTracking()
                .Where(d => d.OrdenCompraId == ordenCompraId)
                .OrderBy(d => d.DetalleId)
                .Select(d => new
                {
                    d.DetalleId, d.IngredienteId, Ingrediente = d.Ingrediente.Nombre,
                    d.Ingrediente.UnidadMedida, d.Cantidad, d.PrecioUnitario, d.CantidadRecibida
                })
                .ToListAsync(ct))
            .Select(d => new PurchaseLineDetail(
                d.DetalleId, d.IngredienteId, d.Ingrediente, d.UnidadMedida,
                d.Cantidad, d.PrecioUnitario, d.CantidadRecibida, d.Cantidad * d.PrecioUnitario))
            .ToList();

        var recepciones = (await _db.RecepcionMercancia.AsNoTracking()
                .Where(r => r.OrdenCompraId == ordenCompraId)
                .OrderBy(r => r.Fecha)
                .Select(r => new { r.RecepcionId, r.Fecha, r.UsuarioId, r.EntregaAtiempo, r.Observaciones })
                .ToListAsync(ct))
            .Select(r => new ReceiptSummary(r.RecepcionId, r.Fecha, r.UsuarioId, r.EntregaAtiempo, r.Observaciones))
            .ToList();

        return new PurchaseOrderDetail(
            o.OrdenCompraId, o.ProveedorId, o.Proveedor, o.UsuarioId, o.Estado, o.FechaCreacion,
            o.FechaEsperada, lineas.Sum(l => l.Subtotal), lineas, recepciones);
    }

    public async Task<PurchaseOrderHeader?> LockAsync(int ordenCompraId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlAsync(
            $"SELECT OrdenCompraId FROM compras.OrdenCompra WITH (UPDLOCK, ROWLOCK) WHERE OrdenCompraId = {ordenCompraId}", ct);

        return await _db.OrdenCompras.AsNoTracking()
            .Where(o => o.OrdenCompraId == ordenCompraId)
            .Select(o => new PurchaseOrderHeader(o.OrdenCompraId, o.Estado, o.FechaEsperada))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<PurchaseLineState>> GetLinesAsync(int ordenCompraId, CancellationToken ct = default)
        => await _db.DetalleOrdenCompras.AsNoTracking()
            .Where(d => d.OrdenCompraId == ordenCompraId)
            .OrderBy(d => d.DetalleId)
            .Select(d => new PurchaseLineState(d.DetalleId, d.IngredienteId, d.Cantidad, d.CantidadRecibida))
            .ToListAsync(ct);

    public async Task<PurchaseOrderDetail> CreateAsync(NewPurchaseOrderData order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var entity = new OrdenCompra
        {
            ProveedorId = order.ProveedorId,
            UsuarioId = order.UsuarioId,
            Estado = PurchaseOrderStatus.Borrador,
            FechaCreacion = DateTime.UtcNow,
            FechaEsperada = order.FechaEsperada
        };
        foreach (var l in order.Lineas)
            entity.DetalleOrdenCompras.Add(new DetalleOrdenCompra
            {
                IngredienteId = l.IngredienteId, Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario, CantidadRecibida = 0m
            });

        _db.OrdenCompras.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetAsync(entity.OrdenCompraId, ct))!;
    }

    public async Task ReplaceAsync(int ordenCompraId, NewPurchaseOrderData order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var entity = await _db.OrdenCompras.Include(o => o.DetalleOrdenCompras)
            .FirstAsync(o => o.OrdenCompraId == ordenCompraId, ct);
        entity.ProveedorId = order.ProveedorId;
        entity.FechaEsperada = order.FechaEsperada;

        _db.DetalleOrdenCompras.RemoveRange(entity.DetalleOrdenCompras.ToList());
        foreach (var l in order.Lineas)
            entity.DetalleOrdenCompras.Add(new DetalleOrdenCompra
            {
                IngredienteId = l.IngredienteId, Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario, CantidadRecibida = 0m
            });

        await _db.SaveChangesAsync(ct);
    }

    public async Task SetStateAsync(int ordenCompraId, string estado, CancellationToken ct = default)
    {
        var entity = await _db.OrdenCompras.FirstAsync(o => o.OrdenCompraId == ordenCompraId, ct);
        entity.Estado = estado;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RegisterReceiptAsync(ReceiptRecord receipt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var entity = await _db.OrdenCompras.Include(o => o.DetalleOrdenCompras)
            .FirstAsync(o => o.OrdenCompraId == receipt.OrdenCompraId, ct);

        foreach (var recibida in receipt.Lineas)
            entity.DetalleOrdenCompras.First(d => d.DetalleId == recibida.DetalleId).CantidadRecibida += recibida.Cantidad;

        _db.RecepcionMercancia.Add(new RecepcionMercancium
        {
            OrdenCompraId = receipt.OrdenCompraId,
            UsuarioId = receipt.UsuarioId,
            Fecha = DateTime.UtcNow,
            EntregaAtiempo = receipt.EntregaATiempo,
            Observaciones = receipt.Observaciones
        });

        if (receipt.OrdenCompleta)
            entity.Estado = PurchaseOrderStatus.Recibida;

        await _db.SaveChangesAsync(ct);
    }
}

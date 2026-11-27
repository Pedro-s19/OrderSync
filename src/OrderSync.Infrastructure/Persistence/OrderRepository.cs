using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Pedidos;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class OrderRepository : IOrderRepository
{
    private readonly OrderSyncDbContext _db;

    public OrderRepository(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<OrderSummary>> ListAsync(OrderFilter filter, CancellationToken ct = default)
    {
        var query = _db.Pedidos.AsNoTracking().AsQueryable();

        if (filter.Estado is not null) query = query.Where(p => p.Estado == filter.Estado);
        if (filter.Canal is not null) query = query.Where(p => p.Canal == filter.Canal);
        if (filter.Desde is not null)
        {
            var desde = filter.Desde.Value.Date;
            query = query.Where(p => p.FechaCreacion >= desde);
        }
        if (filter.Hasta is not null)
        {
            var hastaExclusivo = filter.Hasta.Value.Date.AddDays(1);
            query = query.Where(p => p.FechaCreacion < hastaExclusivo);
        }

        return await query
            .OrderByDescending(p => p.FechaCreacion)
            .Skip((filter.Pagina - 1) * filter.TamanoPagina)
            .Take(filter.TamanoPagina)
            .Select(p => new OrderSummary(
                p.PedidoId, p.Canal, p.Estado, p.Mesa, p.Total, p.FechaCreacion, p.DetallePedidos.Count))
            .ToListAsync(ct);
    }

    public async Task<OrderDetail?> GetAsync(int pedidoId, CancellationToken ct = default)
    {
        var p = await _db.Pedidos.AsNoTracking()
            .Where(x => x.PedidoId == pedidoId)
            .Select(x => new
            {
                x.PedidoId, x.ClienteId, x.UsuarioId, x.Canal, x.Estado, x.Mesa,
                x.DireccionEntrega, x.TelefonoContacto, x.Total, x.FechaCreacion,
                x.FechaEnvioCocina, x.FechaListo
            })
            .FirstOrDefaultAsync(ct);
        if (p is null) return null;

        var lineas = (await _db.DetallePedidos.AsNoTracking()
                .Where(d => d.PedidoId == pedidoId)
                .OrderBy(d => d.DetalleId)
                .Select(d => new { d.DetalleId, d.ProductoId, Producto = d.Producto.Nombre, d.Cantidad, d.PrecioUnitario })
                .ToListAsync(ct))
            .Select(d => new OrderLineDetail(
                d.DetalleId, d.ProductoId, d.Producto, d.Cantidad, d.PrecioUnitario, d.Cantidad * d.PrecioUnitario))
            .ToList();

        return new OrderDetail(
            p.PedidoId, p.ClienteId, p.UsuarioId, p.Canal, p.Estado, p.Mesa, p.DireccionEntrega,
            p.TelefonoContacto, p.Total, p.FechaCreacion, p.FechaEnvioCocina, p.FechaListo, lineas);
    }

    public async Task<OrderHeader?> LockAsync(int pedidoId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlAsync(
            $"SELECT PedidoId FROM pedidos.Pedido WITH (UPDLOCK, ROWLOCK) WHERE PedidoId = {pedidoId}", ct);

        return await _db.Pedidos.AsNoTracking()
            .Where(p => p.PedidoId == pedidoId)
            .Select(p => new OrderHeader(p.PedidoId, p.Estado))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> CustomerIsActiveAsync(int clienteId, CancellationToken ct = default)
        => _db.Clientes.AnyAsync(c => c.ClienteId == clienteId && c.Activo, ct);

    public async Task<OrderDetail> CreateAsync(NewOrderData order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var entity = new Pedido
        {
            ClienteId = order.ClienteId,
            UsuarioId = order.UsuarioId,
            Canal = order.Canal,
            Estado = OrderStatus.Borrador,
            Mesa = order.Mesa,
            DireccionEntrega = order.DireccionEntrega,
            TelefonoContacto = order.TelefonoContacto,
            Total = order.Total,
            FechaCreacion = DateTime.UtcNow
        };
        foreach (var l in order.Lineas)
            entity.DetallePedidos.Add(new DetallePedido
            {
                ProductoId = l.ProductoId, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario
            });

        _db.Pedidos.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetAsync(entity.PedidoId, ct))!;
    }

    public async Task ReplaceAsync(int pedidoId, NewOrderData order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var entity = await _db.Pedidos.Include(p => p.DetallePedidos).FirstAsync(p => p.PedidoId == pedidoId, ct);
        entity.ClienteId = order.ClienteId;
        entity.Canal = order.Canal;
        entity.Mesa = order.Mesa;
        entity.DireccionEntrega = order.DireccionEntrega;
        entity.TelefonoContacto = order.TelefonoContacto;
        entity.Total = order.Total;

        _db.DetallePedidos.RemoveRange(entity.DetallePedidos.ToList());
        foreach (var l in order.Lineas)
            entity.DetallePedidos.Add(new DetallePedido
            {
                ProductoId = l.ProductoId, Cantidad = l.Cantidad, PrecioUnitario = l.PrecioUnitario
            });

        await _db.SaveChangesAsync(ct);
    }

    public async Task SetStateAsync(int pedidoId, string estado, CancellationToken ct = default)
    {
        var entity = await _db.Pedidos.FirstAsync(p => p.PedidoId == pedidoId, ct);
        entity.Estado = estado;
        if (estado == OrderStatus.EnCocina) entity.FechaEnvioCocina = DateTime.UtcNow;
        if (estado == OrderStatus.Listo) entity.FechaListo = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Inventario;

namespace OrderSync.Infrastructure.Persistence;

public sealed class OrderInventoryReader : IOrderInventoryReader
{
    private readonly OrderSyncDbContext _db;

    public OrderInventoryReader(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<OrderInventorySnapshot?> LoadForUpdateAsync(int pedidoId, CancellationToken ct = default)
    {
        // Bloquea el pedido: dos confirmaciones simultáneas no lo procesan dos veces.
        await _db.Database.ExecuteSqlAsync(
           $"SELECT PedidoId FROM pedidos.Pedido WITH (UPDLOCK, ROWLOCK) WHERE PedidoId = {pedidoId}", ct);

        var estado = await _db.Pedidos.AsNoTracking()
            .Where(p => p.PedidoId == pedidoId)
            .Select(p => p.Estado)
            .FirstOrDefaultAsync(ct);
        if (estado is null) return null;

        var lineas = (await _db.DetallePedidos.AsNoTracking()
                .Where(d => d.PedidoId == pedidoId)
                .Select(d => new { d.ProductoId, d.Cantidad })
                .ToListAsync(ct))
            .Select(d => new OrderLine(d.ProductoId, d.Cantidad))
            .ToList();

        var productoIds = lineas.Select(l => l.ProductoId).Distinct().ToList();

        var receta = (await _db.Receta.AsNoTracking()
                .Where(r => productoIds.Contains(r.ProductoId))
                .Select(r => new { r.ProductoId, r.IngredienteId, r.Cantidad })
                .ToListAsync(ct))
            .Select(r => new RecipeItem(r.ProductoId, r.IngredienteId, r.Cantidad))
            .ToList();

        var ingredienteIds = receta.Select(r => r.IngredienteId).Distinct().Order().ToList();

        // Bloquea los ingredientes en orden fijo (evita interbloqueos entre pedidos).
        foreach (var id in ingredienteIds)
        {
            await _db.Database.ExecuteSqlAsync(
                $"SELECT IngredienteId FROM inventario.Ingrediente WITH (UPDLOCK, ROWLOCK) WHERE IngredienteId = {id}", ct);
        }

        var ingredientes = (await _db.Ingredientes.AsNoTracking()
                .Where(i => ingredienteIds.Contains(i.IngredienteId))
                .Select(i => new { i.IngredienteId, i.Nombre, i.UnidadMedida, i.StockActual, i.StockMinimo })
                .ToListAsync(ct))
            .Select(i => new IngredientStock(i.IngredienteId, i.Nombre, i.UnidadMedida, i.StockActual, i.StockMinimo))
            .ToList();

        return new OrderInventorySnapshot(pedidoId, estado, lineas, receta, ingredientes);
    }
}
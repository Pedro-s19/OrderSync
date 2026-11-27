using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Inventario;
using OrderSync.Application.Pedidos;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

/// <summary>Aplica mermas y ajustes: stock + movimiento + alerta, en un solo SaveChanges.</summary>
public sealed class StockMovementStore : IStockMovementStore
{
    private readonly OrderSyncDbContext _db;

    public StockMovementStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task ApplyAsync(StockChange change, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var ingrediente = await _db.Ingredientes.FirstAsync(i => i.IngredienteId == change.IngredienteId, ct);
        ingrediente.StockActual = change.NuevoStock;

        var ahora = DateTime.UtcNow;
        _db.MovimientoInventarios.Add(new MovimientoInventario
        {
            IngredienteId = change.IngredienteId,
            Tipo = change.Tipo,
            Cantidad = change.Delta,
            UsuarioId = change.UsuarioId,
            Motivo = change.Motivo,
            Fecha = ahora
        });

        var abiertas = await _db.AlertaStocks
            .Where(a => a.IngredienteId == change.IngredienteId && a.Estado == AlertStates.Abierta)
            .ToListAsync(ct);

        if (change.AlertaAbierta)
        {
            var abierta = abiertas.FirstOrDefault();
            if (abierta is not null)
            {
                abierta.StockAlMomento = change.NuevoStock;
                abierta.StockMinimo = change.StockMinimo;
            }
            else
            {
                _db.AlertaStocks.Add(new AlertaStock
                {
                    IngredienteId = change.IngredienteId,
                    StockAlMomento = change.NuevoStock,
                    StockMinimo = change.StockMinimo,
                    Estado = AlertStates.Abierta,
                    Fecha = ahora
                });
            }
        }
        else
        {
            foreach (var alerta in abiertas)
            {
                alerta.Estado = AlertStates.Atendida;
                alerta.FechaAtencion = ahora;
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

public sealed class StockAlertStore : IStockAlertStore
{
    private readonly OrderSyncDbContext _db;

    public StockAlertStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<string?> GetStateAsync(int alertaId, CancellationToken ct = default)
        => _db.AlertaStocks.AsNoTracking()
            .Where(a => a.AlertaId == alertaId)
            .Select(a => (string?)a.Estado)
            .FirstOrDefaultAsync(ct);

    public async Task MarkAttendedAsync(int alertaId, CancellationToken ct = default)
    {
        var alerta = await _db.AlertaStocks.FirstAsync(a => a.AlertaId == alertaId, ct);
        alerta.Estado = AlertStates.Atendida;
        alerta.FechaAtencion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class MovementReader : IMovementReader
{
    private readonly OrderSyncDbContext _db;

    public MovementReader(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<MovementDto>> ListAsync(MovementFilter filter, CancellationToken ct = default)
    {
        var query = _db.MovimientoInventarios.AsNoTracking().AsQueryable();

        if (filter.IngredienteId is not null) query = query.Where(m => m.IngredienteId == filter.IngredienteId);
        if (filter.Tipo is not null) query = query.Where(m => m.Tipo == filter.Tipo);
        if (filter.Desde is not null)
        {
            var desde = filter.Desde.Value.Date;
            query = query.Where(m => m.Fecha >= desde);
        }
        if (filter.Hasta is not null)
        {
            var hastaExclusivo = filter.Hasta.Value.Date.AddDays(1);
            query = query.Where(m => m.Fecha < hastaExclusivo);
        }

        return await query
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.MovimientoId)
            .Skip((filter.Pagina - 1) * filter.TamanoPagina)
            .Take(filter.TamanoPagina)
            .Select(m => new MovementDto(
                m.MovimientoId, m.IngredienteId, m.Ingrediente.Nombre, m.Tipo, m.Cantidad,
                m.PedidoId, m.UsuarioId, m.Motivo, m.Fecha))
            .ToListAsync(ct);
    }
}

/// <summary>Cancelación de un pedido confirmado: devuelve al stock lo descontado (FR-21).</summary>
public sealed class InventoryRestorer : IInventoryRestorer
{
    private readonly OrderSyncDbContext _db;

    public InventoryRestorer(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<RestoredIngredient>> RestoreAsync(
        int pedidoId, int? usuarioId, CancellationToken ct = default)
    {
        // Se restituye lo que realmente se descontó (movimientos VENTA), aunque la receta haya cambiado.
        var ventas = await _db.MovimientoInventarios.AsNoTracking()
            .Where(m => m.PedidoId == pedidoId && m.Tipo == MovementTypes.Venta)
            .Select(m => new { m.IngredienteId, m.Cantidad })
            .ToListAsync(ct);

        var porIngrediente = ventas
            .GroupBy(v => v.IngredienteId)
            .Select(g => new { IngredienteId = g.Key, Cantidad = -g.Sum(v => v.Cantidad) })
            .Where(x => x.Cantidad > 0)
            .OrderBy(x => x.IngredienteId)
            .ToList();

        // Bloqueo en orden fijo, igual que al confirmar (evita interbloqueos).
        foreach (var item in porIngrediente)
        {
            await _db.Database.ExecuteSqlAsync(
                $"SELECT IngredienteId FROM inventario.Ingrediente WITH (UPDLOCK, ROWLOCK) WHERE IngredienteId = {item.IngredienteId}", ct);
        }

        var ids = porIngrediente.Select(x => x.IngredienteId).ToList();
        var ingredientes = await _db.Ingredientes.Where(i => ids.Contains(i.IngredienteId))
            .ToDictionaryAsync(i => i.IngredienteId, ct);

        var abiertas = await _db.AlertaStocks
            .Where(a => ids.Contains(a.IngredienteId) && a.Estado == AlertStates.Abierta)
            .ToListAsync(ct);

        var ahora = DateTime.UtcNow;
        var restituidos = new List<RestoredIngredient>();
        foreach (var item in porIngrediente)
        {
            var ingrediente = ingredientes[item.IngredienteId];
            ingrediente.StockActual += item.Cantidad;

            _db.MovimientoInventarios.Add(new MovimientoInventario
            {
                IngredienteId = item.IngredienteId,
                Tipo = MovementTypes.Cancelacion,
                Cantidad = item.Cantidad,
                PedidoId = pedidoId,
                UsuarioId = usuarioId,
                Motivo = $"Cancelación del pedido {pedidoId}",
                Fecha = ahora
            });

            // Si el stock recuperado vuelve a estar en el mínimo o por encima, la alerta deja de aplicar.
            if (ingrediente.StockActual >= ingrediente.StockMinimo)
            {
                foreach (var alerta in abiertas.Where(a => a.IngredienteId == item.IngredienteId))
                {
                    alerta.Estado = AlertStates.Atendida;
                    alerta.FechaAtencion = ahora;
                }
            }

            restituidos.Add(new RestoredIngredient(item.IngredienteId, ingrediente.Nombre, item.Cantidad));
        }

        await _db.SaveChangesAsync(ct);
        return restituidos;
    }
}

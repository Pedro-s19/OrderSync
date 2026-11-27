using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Inventario;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class InventoryWriter : IInventoryWriter
{
    private readonly OrderSyncDbContext _db;

    public InventoryWriter(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task ApplyAsync(int pedidoId, int? usuarioId, DeductionPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var pedido = await _db.Pedidos.FirstAsync(p => p.PedidoId == pedidoId, ct);
        pedido.Estado = OrderStates.Confirmado;

        // 1) Descuento de stock + movimiento VENTA (cantidad negativa) por ingrediente.
        var ids = plan.Descuentos.Select(d => d.IngredienteId).ToList();
        var ingredientes = await _db.Ingredientes
            .Where(i => ids.Contains(i.IngredienteId))
            .ToDictionaryAsync(i => i.IngredienteId, ct);

        var ahora = DateTime.UtcNow;
        foreach (var d in plan.Descuentos)
        {
            ingredientes[d.IngredienteId].StockActual -= d.Cantidad;

            _db.MovimientoInventarios.Add(new MovimientoInventario
            {
                IngredienteId = d.IngredienteId,
                Tipo = "VENTA",
                Cantidad = -d.Cantidad,
                PedidoId = pedidoId,
                UsuarioId = usuarioId,
                Motivo = $"Venta del pedido {pedidoId}",
                Fecha = ahora
            });
        }

        // 2) Alertas: si ya hay una abierta del ingrediente se actualiza; si no, se crea.
        if (plan.Alertas.Count > 0)
        {
            var alertaIds = plan.Alertas.Select(a => a.IngredienteId).ToList();
            var abiertas = await _db.AlertaStocks
                .Where(a => alertaIds.Contains(a.IngredienteId) && a.Estado == AlertStates.Abierta)
                .ToListAsync(ct);

            foreach (var alerta in plan.Alertas)
            {
                var existente = abiertas.FirstOrDefault(a => a.IngredienteId == alerta.IngredienteId);
                if (existente is not null)
                {
                    existente.StockAlMomento = alerta.StockAlMomento;
                    existente.StockMinimo = alerta.StockMinimo;
                }
                else
                {
                    _db.AlertaStocks.Add(new AlertaStock
                    {
                        IngredienteId = alerta.IngredienteId,
                        StockAlMomento = alerta.StockAlMomento,
                        StockMinimo = alerta.StockMinimo,
                        Estado = AlertStates.Abierta,
                        Fecha = ahora
                    });
                }
            }
        }

        // Un solo SaveChanges: pedido + stock + movimientos + alertas en la misma transacción.
        await _db.SaveChangesAsync(ct);
    }
}
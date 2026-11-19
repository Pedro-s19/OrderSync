namespace OrderSync.Application.Inventario;

/// <summary>Lee el pedido, sus recetas y los stocks, bloqueando las filas (dentro de la transacción).</summary>
public interface IOrderInventoryReader
{
    Task<OrderInventorySnapshot?> LoadForUpdateAsync(int pedidoId, CancellationToken ct = default);
}

/// <summary>Aplica el plan: descuenta stock, registra movimientos y alertas, confirma el pedido.</summary>
public interface IInventoryWriter
{
    Task ApplyAsync(int pedidoId, int? usuarioId, DeductionPlan plan, CancellationToken ct = default);
}

public interface IStockAlertReader
{
    Task<IReadOnlyList<StockAlertDto>> GetAsync(string? estado, CancellationToken ct = default);
}

/// <summary>Regla pura: qué descontar y qué alertas generar.</summary>
public interface IInventoryDeductionPlanner
{
    DeductionPlan Plan(OrderInventorySnapshot snapshot);
}
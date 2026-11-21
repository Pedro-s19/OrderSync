namespace OrderSync.Application.Pedidos;

public interface IOrderRepository
{
    Task<IReadOnlyList<OrderSummary>> ListAsync(OrderFilter filter, CancellationToken ct = default);
    Task<OrderDetail?> GetAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>Bloquea el pedido (dentro de una transacción) y devuelve su estado actual.</summary>
    Task<OrderHeader?> LockAsync(int pedidoId, CancellationToken ct = default);

    Task<bool> CustomerIsActiveAsync(int clienteId, CancellationToken ct = default);
    Task<OrderDetail> CreateAsync(NewOrderData order, CancellationToken ct = default);

    /// <summary>Reemplaza encabezado, líneas y total de un pedido en BORRADOR.</summary>
    Task ReplaceAsync(int pedidoId, NewOrderData order, CancellationToken ct = default);

    Task SetStateAsync(int pedidoId, string estado, CancellationToken ct = default);
}

public interface IProductStore
{
    Task<IReadOnlyList<ProductInfo>> ListAsync(bool incluirInactivos, CancellationToken ct = default);
    Task<ProductInfo?> GetAsync(int productoId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductInfo>> GetManyAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string nombre, int? excludeId, CancellationToken ct = default);
    Task<ProductInfo> CreateAsync(ProductRequest product, CancellationToken ct = default);
    Task UpdateAsync(int productoId, ProductRequest product, CancellationToken ct = default);
    Task SetActiveAsync(int productoId, bool activo, CancellationToken ct = default);
}

/// <summary>Devuelve al stock lo que descontó un pedido confirmado (FR-21).</summary>
public interface IInventoryRestorer
{
    Task<IReadOnlyList<RestoredIngredient>> RestoreAsync(int pedidoId, int? usuarioId, CancellationToken ct = default);
}

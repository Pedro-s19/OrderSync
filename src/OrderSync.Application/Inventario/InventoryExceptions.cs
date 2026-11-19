namespace OrderSync.Application.Inventario;

public sealed class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int pedidoId) : base($"No existe el pedido {pedidoId}.")
        => PedidoId = pedidoId;

    public int PedidoId { get; }
}

public sealed class InvalidOrderStateException : Exception
{
    public InvalidOrderStateException(int pedidoId, string estado)
        : base($"El pedido {pedidoId} está en estado {estado}; solo se confirma un pedido en BORRADOR.")
    {
        PedidoId = pedidoId;
        Estado = estado;
    }

    public int PedidoId { get; }
    public string Estado { get; }
}

public sealed class EmptyOrderException : Exception
{
    public EmptyOrderException(int pedidoId) : base($"El pedido {pedidoId} no tiene productos.")
        => PedidoId = pedidoId;

    public int PedidoId { get; }
}

public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(int pedidoId, IReadOnlyList<StockShortage> faltantes)
        : base($"Stock insuficiente para confirmar el pedido {pedidoId}: " +
               string.Join("; ", faltantes.Select(f => $"{f.Nombre} (necesita {f.Requerido}, hay {f.Disponible})")))
    {
        PedidoId = pedidoId;
        Faltantes = faltantes;
    }

    public int PedidoId { get; }
    public IReadOnlyList<StockShortage> Faltantes { get; }
}
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;

namespace OrderSync.Application.Pedidos;

/// <summary>
/// CRUD de pedidos y su ciclo de vida: BORRADOR -> CONFIRMADO -> (EN_COCINA) -> LISTO -> ENTREGADO,
/// con CANCELADO como "desactivar". La confirmación con descuento de inventario vive en
/// OrderConfirmationService. Los precios los pone el servidor, nunca el cliente.
/// </summary>
public sealed class OrderService
{
    private const int MaxQuantityPerLine = 1000;

    private readonly IOrderRepository _orders;
    private readonly IProductStore _products;
    private readonly IInventoryRestorer _restorer;
    private readonly ITransactionRunner _tx;

    public OrderService(
        IOrderRepository orders, IProductStore products, IInventoryRestorer restorer, ITransactionRunner tx)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _restorer = restorer ?? throw new ArgumentNullException(nameof(restorer));
        _tx = tx ?? throw new ArgumentNullException(nameof(tx));
    }

    public Task<IReadOnlyList<OrderSummary>> ListAsync(OrderFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var estado = Normalize(filter.Estado);
        var canal = Normalize(filter.Canal);

        if (estado is not null && !OrderStatus.All.Contains(estado))
            throw new RequestValidationException($"Estado no válido. Use: {string.Join(", ", OrderStatus.All)}.");
        if (canal is not null && !OrderChannels.All.Contains(canal))
            throw new RequestValidationException($"Canal no válido. Use: {string.Join(", ", OrderChannels.All)}.");
        if (filter.Pagina < 1 || filter.TamanoPagina < 1 || filter.TamanoPagina > 200)
            throw new RequestValidationException("La página debe ser 1 o más y el tamaño entre 1 y 200.");
        if (filter.Desde > filter.Hasta)
            throw new RequestValidationException("La fecha 'desde' no puede ser posterior a 'hasta'.");

        return _orders.ListAsync(filter with { Estado = estado, Canal = canal }, ct);
    }

    public async Task<OrderDetail> GetAsync(int pedidoId, CancellationToken ct = default)
        => await _orders.GetAsync(pedidoId, ct) ?? throw new OrderNotFoundException(pedidoId);

    public async Task<OrderDetail> CreateAsync(int? usuarioId, OrderRequest request, CancellationToken ct = default)
    {
        var data = await BuildAsync(request, usuarioId, ct);
        return await _orders.CreateAsync(data, ct);
    }

    public Task<OrderDetail> UpdateAsync(int pedidoId, OrderRequest request, CancellationToken ct = default)
        => _tx.ExecuteAsync(async token =>
        {
            var header = await _orders.LockAsync(pedidoId, token) ?? throw new OrderNotFoundException(pedidoId);
            if (header.Estado != OrderStatus.Borrador)
                throw new ConflictException(
                    $"El pedido {pedidoId} está en estado {header.Estado}; solo se edita un pedido en BORRADOR.");

            var data = await BuildAsync(request, null, token);
            await _orders.ReplaceAsync(pedidoId, data, token);
            return await _orders.GetAsync(pedidoId, token) ?? throw new OrderNotFoundException(pedidoId);
        }, ct);

    /// <summary>Cocina marca el pedido como listo (FR-05).</summary>
    public Task<OrderDetail> MarkReadyAsync(int pedidoId, CancellationToken ct = default)
        => TransitionAsync(pedidoId, "marcar como listo",
            [OrderStatus.Confirmado, OrderStatus.EnCocina], OrderStatus.Listo, ct);

    public Task<OrderDetail> DeliverAsync(int pedidoId, CancellationToken ct = default)
        => TransitionAsync(pedidoId, "entregar", [OrderStatus.Listo], OrderStatus.Entregado, ct);

    /// <summary>
    /// Cancela un pedido en BORRADOR o CONFIRMADO. Si estaba confirmado, devuelve los
    /// ingredientes al inventario y lo deja registrado (FR-21).
    /// </summary>
    public Task<OrderCancellationResult> CancelAsync(int pedidoId, int? usuarioId, CancellationToken ct = default)
        => _tx.ExecuteAsync(async token =>
        {
            var header = await _orders.LockAsync(pedidoId, token) ?? throw new OrderNotFoundException(pedidoId);

            IReadOnlyList<RestoredIngredient> restituidos = [];
            if (header.Estado == OrderStatus.Confirmado)
                restituidos = await _restorer.RestoreAsync(pedidoId, usuarioId, token);
            else if (header.Estado != OrderStatus.Borrador)
                throw new ConflictException(
                    $"El pedido {pedidoId} está en estado {header.Estado}; solo se cancela en BORRADOR o CONFIRMADO.");

            await _orders.SetStateAsync(pedidoId, OrderStatus.Cancelado, token);
            return new OrderCancellationResult(pedidoId, OrderStatus.Cancelado, restituidos);
        }, ct);

    private Task<OrderDetail> TransitionAsync(
        int pedidoId, string accion, string[] estadosPermitidos, string nuevoEstado, CancellationToken ct)
        => _tx.ExecuteAsync(async token =>
        {
            var header = await _orders.LockAsync(pedidoId, token) ?? throw new OrderNotFoundException(pedidoId);
            if (!estadosPermitidos.Contains(header.Estado))
                throw new ConflictException(
                    $"El pedido {pedidoId} está en estado {header.Estado}; no se puede {accion}.");

            await _orders.SetStateAsync(pedidoId, nuevoEstado, token);
            return await _orders.GetAsync(pedidoId, token) ?? throw new OrderNotFoundException(pedidoId);
        }, ct);

    private async Task<NewOrderData> BuildAsync(OrderRequest request, int? usuarioId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var canal = Normalize(request.Canal);
        if (canal is null || !OrderChannels.All.Contains(canal))
            throw new RequestValidationException($"Canal no válido. Use: {string.Join(", ", OrderChannels.All)}.");

        var mesa = Clean(request.Mesa, 10, "mesa");
        var direccion = Clean(request.DireccionEntrega, 200, "dirección de entrega");
        var telefono = Clean(request.TelefonoContacto, 20, "teléfono de contacto");

        // FR-02: el domicilio exige dirección y contacto. FR-03: el pedido por QR sale de una mesa.
        if (canal == OrderChannels.Domicilio && (direccion is null || telefono is null))
            throw new RequestValidationException(
                "Un pedido a domicilio exige dirección de entrega y teléfono de contacto.");
        if (canal == OrderChannels.Qr && mesa is null)
            throw new RequestValidationException("Un pedido por QR exige el número de mesa.");

        if (request.Lineas is null || request.Lineas.Count == 0)
            throw new RequestValidationException("El pedido debe tener al menos un producto.");
        if (request.Lineas.Any(l => l.Cantidad < 1))
            throw new RequestValidationException("La cantidad de cada producto debe ser 1 o más.");

        var lineas = request.Lineas
            .GroupBy(l => l.ProductoId)
            .Select(g => new OrderLineInput(g.Key, g.Sum(l => l.Cantidad)))
            .ToList();
        if (lineas.Any(l => l.Cantidad > MaxQuantityPerLine))
            throw new RequestValidationException($"La cantidad máxima por producto es {MaxQuantityPerLine}.");

        var productos = (await _products.GetManyAsync(lineas.Select(l => l.ProductoId).ToList(), ct))
            .ToDictionary(p => p.ProductoId);

        var priced = new List<PricedLine>();
        foreach (var linea in lineas)
        {
            if (!productos.TryGetValue(linea.ProductoId, out var producto))
                throw new ResourceNotFoundException($"No existe el producto {linea.ProductoId}.");
            if (!producto.Activo)
                throw new BusinessRuleException($"El producto {producto.Nombre} está desactivado.");

            priced.Add(new PricedLine(linea.ProductoId, linea.Cantidad, producto.Precio));
        }

        if (request.ClienteId is int clienteId && !await _orders.CustomerIsActiveAsync(clienteId, ct))
            throw new BusinessRuleException($"El cliente {clienteId} no existe o está desactivado.");

        var total = priced.Sum(l => l.Cantidad * l.PrecioUnitario);
        return new NewOrderData(request.ClienteId, usuarioId, canal, mesa, direccion, telefono, total, priced);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? Clean(string? value, int maxLength, string campo)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new RequestValidationException($"La {campo} admite hasta {maxLength} caracteres.");
        return trimmed;
    }
}

using OrderSync.Application.Common;
using OrderSync.Application.Inventario;

namespace OrderSync.Application.Proveedores;

/// <summary>
/// Órdenes de compra (FR-08): BORRADOR -> ENVIADA -> RECIBIDA, con CANCELADA como "desactivar".
/// La recepción de mercancía (FR-09) suma el stock al instante y deja movimientos RECEPCION,
/// todo en una sola transacción con la orden y los ingredientes bloqueados.
/// </summary>
public sealed class PurchaseOrderService
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly ISupplierStore _suppliers;
    private readonly IIngredientStore _ingredients;
    private readonly IStockMovementStore _movements;
    private readonly ITransactionRunner _tx;
    private readonly IClock _clock;

    public PurchaseOrderService(
        IPurchaseOrderRepository orders, ISupplierStore suppliers, IIngredientStore ingredients,
        IStockMovementStore movements, ITransactionRunner tx, IClock clock)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _ingredients = ingredients ?? throw new ArgumentNullException(nameof(ingredients));
        _movements = movements ?? throw new ArgumentNullException(nameof(movements));
        _tx = tx ?? throw new ArgumentNullException(nameof(tx));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<IReadOnlyList<PurchaseOrderSummary>> ListAsync(PurchaseOrderFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var estado = string.IsNullOrWhiteSpace(filter.Estado) ? null : filter.Estado.Trim().ToUpperInvariant();
        if (estado is not null && !PurchaseOrderStatus.All.Contains(estado))
            throw new RequestValidationException($"Estado no válido. Use: {string.Join(", ", PurchaseOrderStatus.All)}.");
        InputRules.Paging(filter.Pagina, filter.TamanoPagina);

        return _orders.ListAsync(filter with { Estado = estado }, ct);
    }

    public async Task<PurchaseOrderDetail> GetAsync(int ordenCompraId, CancellationToken ct = default)
        => await _orders.GetAsync(ordenCompraId, ct) ?? throw NotFound(ordenCompraId);

    public async Task<PurchaseOrderDetail> CreateAsync(int? usuarioId, PurchaseOrderRequest request, CancellationToken ct = default)
    {
        var data = await BuildAsync(request, usuarioId, ct);
        return await _orders.CreateAsync(data, ct);
    }

    public Task<PurchaseOrderDetail> UpdateAsync(int ordenCompraId, PurchaseOrderRequest request, CancellationToken ct = default)
        => _tx.ExecuteAsync(async token =>
        {
            var header = await _orders.LockAsync(ordenCompraId, token) ?? throw NotFound(ordenCompraId);
            if (header.Estado != PurchaseOrderStatus.Borrador)
                throw new ConflictException(
                    $"La orden {ordenCompraId} está en estado {header.Estado}; solo se edita en BORRADOR.");

            var data = await BuildAsync(request, null, token);
            await _orders.ReplaceAsync(ordenCompraId, data, token);
            return await _orders.GetAsync(ordenCompraId, token) ?? throw NotFound(ordenCompraId);
        }, ct);

    public Task<PurchaseOrderDetail> SendAsync(int ordenCompraId, CancellationToken ct = default)
        => _tx.ExecuteAsync(async token =>
        {
            var header = await _orders.LockAsync(ordenCompraId, token) ?? throw NotFound(ordenCompraId);
            if (header.Estado != PurchaseOrderStatus.Borrador)
                throw new ConflictException(
                    $"La orden {ordenCompraId} está en estado {header.Estado}; solo se envía desde BORRADOR.");

            var detail = await _orders.GetAsync(ordenCompraId, token) ?? throw NotFound(ordenCompraId);
            var supplier = await _suppliers.GetAsync(detail.ProveedorId, token);
            if (supplier is null || !supplier.Activo)
                throw new BusinessRuleException("El proveedor de la orden está desactivado; no se puede enviar.");

            await _orders.SetStateAsync(ordenCompraId, PurchaseOrderStatus.Enviada, token);
            return await _orders.GetAsync(ordenCompraId, token) ?? throw NotFound(ordenCompraId);
        }, ct);

    public Task<PurchaseOrderDetail> CancelAsync(int ordenCompraId, CancellationToken ct = default)
        => _tx.ExecuteAsync(async token =>
        {
            var header = await _orders.LockAsync(ordenCompraId, token) ?? throw NotFound(ordenCompraId);
            if (header.Estado is not (PurchaseOrderStatus.Borrador or PurchaseOrderStatus.Enviada))
                throw new ConflictException(
                    $"La orden {ordenCompraId} está en estado {header.Estado}; solo se cancela en BORRADOR o ENVIADA.");

            if (header.Estado == PurchaseOrderStatus.Enviada
                && (await _orders.GetLinesAsync(ordenCompraId, token)).Any(l => l.CantidadRecibida > 0))
                throw new ConflictException($"La orden {ordenCompraId} ya tiene mercancía recibida; no se puede cancelar.");

            await _orders.SetStateAsync(ordenCompraId, PurchaseOrderStatus.Cancelada, token);
            return await _orders.GetAsync(ordenCompraId, token) ?? throw NotFound(ordenCompraId);
        }, ct);

    public Task<ReceiptResult> ReceiveAsync(
        int? usuarioId, int ordenCompraId, ReceiptRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Lineas is null || request.Lineas.Count == 0)
            throw new RequestValidationException("La recepción debe incluir al menos una línea.");
        if (request.Lineas.Any(l => l.Cantidad <= 0))
            throw new RequestValidationException("La cantidad recibida de cada línea debe ser mayor que cero.");
        if (request.Lineas.Select(l => l.DetalleId).Distinct().Count() != request.Lineas.Count)
            throw new RequestValidationException("Una línea no puede repetirse en la misma recepción.");
        var observaciones = InputRules.Optional(request.Observaciones, 300, "Las observaciones");

        return _tx.ExecuteAsync(async token =>
        {
            var header = await _orders.LockAsync(ordenCompraId, token) ?? throw NotFound(ordenCompraId);
            if (header.Estado != PurchaseOrderStatus.Enviada)
                throw new ConflictException(
                    $"La orden {ordenCompraId} está en estado {header.Estado}; solo se recibe mercancía de una orden ENVIADA.");

            var estados = (await _orders.GetLinesAsync(ordenCompraId, token)).ToDictionary(l => l.DetalleId);

            // Total que entra por ingrediente, en orden de id (mismo orden de bloqueo que el resto del sistema).
            var porIngrediente = new SortedDictionary<int, decimal>();
            foreach (var recibida in request.Lineas)
            {
                if (!estados.TryGetValue(recibida.DetalleId, out var linea))
                    throw new RequestValidationException(
                        $"La línea {recibida.DetalleId} no pertenece a la orden {ordenCompraId}.");

                var pendiente = linea.Cantidad - linea.CantidadRecibida;
                if (recibida.Cantidad > pendiente)
                    throw new BusinessRuleException(
                        $"La cantidad recibida ({recibida.Cantidad}) supera lo pendiente ({pendiente}) de la línea {recibida.DetalleId}.");

                porIngrediente.TryGetValue(linea.IngredienteId, out var acumulado);
                porIngrediente[linea.IngredienteId] = acumulado + recibida.Cantidad;
            }

            var ingresos = new List<StockChangeResult>();
            foreach (var (ingredienteId, cantidad) in porIngrediente)
            {
                var stock = await _ingredients.LockAsync(ingredienteId, token)
                            ?? throw new ResourceNotFoundException($"No existe el ingrediente {ingredienteId}.");

                var nuevo = stock.StockActual + cantidad;
                var bajoMinimo = nuevo < stock.StockMinimo;

                await _movements.ApplyAsync(new StockChange(
                    ingredienteId, MovementTypes.Recepcion, cantidad, nuevo, stock.StockMinimo, bajoMinimo,
                    usuarioId, $"Recepción de la orden de compra {ordenCompraId}"), token);

                ingresos.Add(new StockChangeResult(
                    ingredienteId, stock.Nombre, MovementTypes.Recepcion, cantidad,
                    stock.StockActual, nuevo, stock.StockMinimo, bajoMinimo));
            }

            var completa = estados.Values.All(l =>
                l.CantidadRecibida + request.Lineas.Where(r => r.DetalleId == l.DetalleId).Sum(r => r.Cantidad) >= l.Cantidad);

            var aTiempo = header.FechaEsperada is DateOnly esperada
                ? DateOnly.FromDateTime(_clock.LocalNow) <= esperada
                : (bool?)null;

            await _orders.RegisterReceiptAsync(
                new ReceiptRecord(ordenCompraId, usuarioId, aTiempo, observaciones, request.Lineas, completa), token);

            return new ReceiptResult(
                ordenCompraId, completa ? PurchaseOrderStatus.Recibida : PurchaseOrderStatus.Enviada, ingresos);
        }, ct);
    }

    private async Task<NewPurchaseOrderData> BuildAsync(PurchaseOrderRequest request, int? usuarioId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var supplier = await _suppliers.GetAsync(request.ProveedorId, ct)
                       ?? throw new ResourceNotFoundException($"No existe el proveedor {request.ProveedorId}.");
        // FR-23: un proveedor desactivado no aparece en órdenes de compra nuevas.
        if (!supplier.Activo)
            throw new BusinessRuleException($"El proveedor {supplier.Nombre} está desactivado.");

        if (request.FechaEsperada is DateOnly esperada && esperada < DateOnly.FromDateTime(_clock.LocalNow))
            throw new RequestValidationException("La fecha esperada no puede ser anterior a hoy.");

        if (request.Lineas is null || request.Lineas.Count == 0)
            throw new RequestValidationException("La orden debe tener al menos un ingrediente.");
        if (request.Lineas.Any(l => l.Cantidad <= 0))
            throw new RequestValidationException("La cantidad de cada ingrediente debe ser mayor que cero.");
        if (request.Lineas.Any(l => l.PrecioUnitario is <= 0))
            throw new RequestValidationException("El precio unitario, si se indica, debe ser mayor que cero.");

        var lineas = request.Lineas
            .GroupBy(l => l.IngredienteId)
            .Select(g => new PurchaseLineInput(
                g.Key, g.Sum(l => l.Cantidad), g.Select(l => l.PrecioUnitario).FirstOrDefault(p => p is not null)))
            .ToList();
        var ids = lineas.Select(l => l.IngredienteId).ToList();

        var ingredientes = (await _ingredients.GetManyAsync(ids, ct)).ToDictionary(i => i.IngredienteId);
        var precios = await _suppliers.GetPriceMapAsync(request.ProveedorId, ids, ct);

        var priced = new List<PurchasePricedLine>();
        foreach (var linea in lineas)
        {
            if (!ingredientes.TryGetValue(linea.IngredienteId, out var ingrediente))
                throw new ResourceNotFoundException($"No existe el ingrediente {linea.IngredienteId}.");
            if (!ingrediente.Activo)
                throw new BusinessRuleException($"El ingrediente {ingrediente.Nombre} está desactivado.");

            // El precio de la lista del proveedor se usa si el cliente no indica otro.
            var precio = linea.PrecioUnitario;
            if (precio is null)
            {
                if (!precios.TryGetValue(linea.IngredienteId, out var delProveedor))
                    throw new RequestValidationException(
                        $"El proveedor no tiene precio para {ingrediente.Nombre}; indique el precioUnitario.");
                precio = delProveedor;
            }

            priced.Add(new PurchasePricedLine(linea.IngredienteId, linea.Cantidad, precio.Value));
        }

        return new NewPurchaseOrderData(request.ProveedorId, usuarioId, request.FechaEsperada, priced);
    }

    private static ResourceNotFoundException NotFound(int ordenCompraId)
        => new($"No existe la orden de compra {ordenCompraId}.");
}

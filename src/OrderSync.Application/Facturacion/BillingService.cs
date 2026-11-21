using OrderSync.Application.Common;
using OrderSync.Application.Crm;
using OrderSync.Application.Inventario;
using OrderSync.Application.Pedidos;

namespace OrderSync.Application.Facturacion;

/// <summary>
/// Cobro de un pedido (FR-10): la factura existe solo cuando el pago se confirma. En una sola
/// transacción aplica la promoción que corresponda (FR-17), guarda factura y pagos, y acumula
/// los puntos de fidelidad del cliente (FR-16). La propina va aparte del total del pedido (FR-26).
/// </summary>
public sealed class BillingService
{
    /// <summary>Un punto por cada $1.000 del valor pagado por el pedido (sin propina).</summary>
    public const decimal PesosPerPoint = 1000m;

    private static readonly string[] ChargeableStates =
        [OrderStatus.Confirmado, OrderStatus.EnCocina, OrderStatus.Listo, OrderStatus.Entregado];

    private readonly ITransactionRunner _tx;
    private readonly IOrderRepository _orders;
    private readonly IInvoiceStore _invoices;
    private readonly ICustomerStore _customers;
    private readonly IPromotionStore _promotions;
    private readonly IClock _clock;

    public BillingService(
        ITransactionRunner tx, IOrderRepository orders, IInvoiceStore invoices,
        ICustomerStore customers, IPromotionStore promotions, IClock clock)
    {
        _tx = tx ?? throw new ArgumentNullException(nameof(tx));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _promotions = promotions ?? throw new ArgumentNullException(nameof(promotions));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var propina = request.Propina ?? 0m;
        if (propina < 0)
            throw new RequestValidationException("La propina no puede ser negativa.");
        if (request.Pagos is null || request.Pagos.Count == 0)
            throw new RequestValidationException("Debe indicar al menos un pago.");

        var pagos = new List<PaymentInput>();
        foreach (var pago in request.Pagos)
        {
            var metodo = string.IsNullOrWhiteSpace(pago.Metodo) ? null : pago.Metodo.Trim().ToUpperInvariant();
            if (metodo is null || !PaymentMethods.All.Contains(metodo))
                throw new RequestValidationException($"Método de pago no válido. Use: {string.Join(", ", PaymentMethods.All)}.");
            if (pago.Monto <= 0)
                throw new RequestValidationException("El monto de cada pago debe ser mayor que cero.");
            pagos.Add(new PaymentInput(metodo, pago.Monto));
        }

        return _tx.ExecuteAsync(async token =>
        {
            var header = await _orders.LockAsync(request.PedidoId, token)
                         ?? throw new OrderNotFoundException(request.PedidoId);
            if (!ChargeableStates.Contains(header.Estado))
                throw new ConflictException(
                    $"El pedido {request.PedidoId} está en estado {header.Estado}; se cobra un pedido CONFIRMADO, EN_COCINA, LISTO o ENTREGADO.");
            if (await _invoices.ExistsForOrderAsync(request.PedidoId, token))
                throw new ConflictException($"El pedido {request.PedidoId} ya tiene factura.");

            var order = await _orders.GetAsync(request.PedidoId, token)
                        ?? throw new OrderNotFoundException(request.PedidoId);

            // Promoción automática: solo para un cliente activo del pedido.
            CustomerDto? cliente = null;
            PromotionDto? promocion = null;
            if (order.ClienteId is int clienteId)
            {
                var ficha = await _customers.LockAsync(clienteId, token);
                if (ficha is { Activo: true })
                {
                    cliente = ficha;
                    var asignadas = await _promotions.GetAssignedAsync(clienteId, token);
                    promocion = PromotionPolicy.SelectBest(
                        asignadas, ficha.PuntosFidelidad, DateOnly.FromDateTime(_clock.LocalNow));
                }
            }

            var subtotal = order.Total;
            var descuento = promocion is null
                ? 0m
                : Math.Round(subtotal * promocion.PorcentajeDto / 100m, 2, MidpointRounding.AwayFromZero);
            var total = subtotal - descuento + propina;

            var pagado = pagos.Sum(p => p.Monto);
            if (pagado != total)
                throw new BusinessRuleException($"El total a pagar es {total} y los pagos suman {pagado}.");

            var factura = await _invoices.CreateAsync(
                new NewInvoiceData(request.PedidoId, subtotal, propina, total, pagos), token);

            var puntos = 0;
            if (cliente is not null)
            {
                puntos = (int)Math.Floor((subtotal - descuento) / PesosPerPoint);
                if (puntos > 0)
                    await _customers.AddPointsAsync(cliente.ClienteId, request.PedidoId, puntos, token);
            }

            return new ChargeResult(factura, promocion?.Nombre, promocion?.PorcentajeDto ?? 0m, puntos);
        }, ct);
    }

    public async Task<InvoiceDto> GetAsync(int facturaId, CancellationToken ct = default)
        => await _invoices.GetAsync(facturaId, ct)
           ?? throw new ResourceNotFoundException($"No existe la factura {facturaId}.");

    public async Task<InvoiceDto> GetByOrderAsync(int pedidoId, CancellationToken ct = default)
        => await _invoices.GetByOrderAsync(pedidoId, ct)
           ?? throw new ResourceNotFoundException($"El pedido {pedidoId} no tiene factura.");

    /// <summary>Lista facturas. Las fechas son locales (Colombia) y se convierten a UTC para consultar.</summary>
    public Task<IReadOnlyList<InvoiceSummary>> ListAsync(InvoiceFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        InputRules.Paging(filter.Pagina, filter.TamanoPagina);
        InputRules.DateRange(filter.Desde, filter.Hasta);

        var desdeUtc = filter.Desde is DateTime d ? _clock.ToUtc(d.Date) : (DateTime?)null;
        var hastaUtc = filter.Hasta is DateTime h ? _clock.ToUtc(h.Date.AddDays(1)) : (DateTime?)null;

        return _invoices.ListAsync(new InvoiceQuery(desdeUtc, hastaUtc, filter.Pagina, filter.TamanoPagina), ct);
    }
}

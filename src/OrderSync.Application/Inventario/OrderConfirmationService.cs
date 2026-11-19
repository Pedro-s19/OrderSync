using OrderSync.Application.Common;

namespace OrderSync.Application.Inventario;

/// <summary>
/// Caso de uso "confirmar pedido": descuenta inventario por receta en UNA transacción.
/// Si algo falla, no queda ningún descuento a medias (NFR-02).
/// </summary>
public sealed class OrderConfirmationService
{
    private readonly ITransactionRunner _transaction;
    private readonly IOrderInventoryReader _reader;
    private readonly IInventoryDeductionPlanner _planner;
    private readonly IInventoryWriter _writer;

    public OrderConfirmationService(
        ITransactionRunner transaction,
        IOrderInventoryReader reader,
        IInventoryDeductionPlanner planner,
        IInventoryWriter writer)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public Task<OrderConfirmationResult> ConfirmAsync(
        int pedidoId, int? usuarioId = null, CancellationToken ct = default)
        => _transaction.ExecuteAsync(async token =>
        {
            var snapshot = await _reader.LoadForUpdateAsync(pedidoId, token)
                           ?? throw new OrderNotFoundException(pedidoId);

            if (snapshot.Estado != OrderStates.Borrador)
                throw new InvalidOrderStateException(pedidoId, snapshot.Estado);

            var plan = _planner.Plan(snapshot);
            await _writer.ApplyAsync(pedidoId, usuarioId, plan, token);

            return new OrderConfirmationResult(
                pedidoId, OrderStates.Confirmado, plan.Descuentos, plan.Alertas);
        }, ct);
}
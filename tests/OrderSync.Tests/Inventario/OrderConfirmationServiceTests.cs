using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using Xunit;

namespace OrderSync.Tests.Inventario;

internal sealed class PassThroughTransactionRunner : ITransactionRunner
{
    public int Executions { get; private set; }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        Executions++;
        return await action(ct);
    }
}

public class OrderConfirmationServiceTests
{
    private readonly PassThroughTransactionRunner _tx = new();
    private readonly Mock<IOrderInventoryReader> _reader = new();
    private readonly Mock<IInventoryDeductionPlanner> _planner = new();
    private readonly Mock<IInventoryWriter> _writer = new();
    private readonly List<string> _calls = new();

    private OrderConfirmationService CreateService()
        => new(_tx, _reader.Object, _planner.Object, _writer.Object);

    private static OrderInventorySnapshot Snapshot(string estado = "BORRADOR")
        => new(5, estado, [new OrderLine(1, 2)], [], []);

    private static DeductionPlan Plan() => new(
        [new IngredientDeduction(1, "Pan", "und", 2m, 21m, 19m)],
        [new StockAlertDraft(1, "Pan", "und", 19m, 20m)]);

    private void GivenOrder(OrderInventorySnapshot? snapshot)
        => _reader.Setup(r => r.LoadForUpdateAsync(5, It.IsAny<CancellationToken>()))
                  .Callback(() => _calls.Add("load"))
                  .ReturnsAsync(snapshot);

    private void GivenPlan(DeductionPlan plan)
        => _planner.Setup(p => p.Plan(It.IsAny<OrderInventorySnapshot>()))
                   .Callback(() => _calls.Add("plan"))
                   .Returns(plan);

    private void GivenWriter()
        => _writer.Setup(w => w.ApplyAsync(
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DeductionPlan>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("apply"))
            .Returns(Task.CompletedTask);

    private void VerifyWriterNeverCalled()
        => _writer.Verify(w => w.ApplyAsync(
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<DeductionPlan>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Fact]
    public async Task ConfirmaElPedido_YDevuelveDescuentosYAlertas()
    {
        var plan = Plan();
        GivenOrder(Snapshot());
        GivenPlan(plan);
        GivenWriter();

        var result = await CreateService().ConfirmAsync(5);

        Assert.Equal(5, result.PedidoId);
        Assert.Equal("CONFIRMADO", result.Estado);
        Assert.Same(plan.Descuentos, result.Descuentos);
        Assert.Same(plan.Alertas, result.Alertas);
    }

    [Fact]
    public async Task LeePlanificaYEscribe_EnOrden_DentroDeUnaSolaTransaccion()
    {
        GivenOrder(Snapshot());
        GivenPlan(Plan());
        GivenWriter();

        await CreateService().ConfirmAsync(5);

        Assert.Equal(1, _tx.Executions);
        Assert.Equal(new[] { "load", "plan", "apply" }, _calls);
    }

    [Fact]
    public async Task PedidoInexistente_LanzaNotFound_YNoEscribe()
    {
        GivenOrder(null);

        await Assert.ThrowsAsync<OrderNotFoundException>(() => CreateService().ConfirmAsync(5));

        VerifyWriterNeverCalled();
    }

    [Theory]
    [InlineData("CONFIRMADO")]
    [InlineData("CANCELADO")]
    [InlineData("ENTREGADO")]
    [InlineData("EN_COCINA")]
    public async Task PedidoQueNoEstaEnBorrador_LanzaInvalidState_YNoEscribe(string estado)
    {
        GivenOrder(Snapshot(estado));

        var ex = await Assert.ThrowsAsync<InvalidOrderStateException>(() => CreateService().ConfirmAsync(5));

        Assert.Equal(estado, ex.Estado);
        _planner.Verify(p => p.Plan(It.IsAny<OrderInventorySnapshot>()), Times.Never);
        VerifyWriterNeverCalled();
    }

    [Fact]
    public async Task StockInsuficiente_PropagaElErrorYNoEscribeNada()
    {
        GivenOrder(Snapshot());
        _planner.Setup(p => p.Plan(It.IsAny<OrderInventorySnapshot>()))
                .Throws(new InsufficientStockException(5, [new StockShortage(1, "Pan", 2m, 1m)]));

        await Assert.ThrowsAsync<InsufficientStockException>(() => CreateService().ConfirmAsync(5));

        VerifyWriterNeverCalled();
    }

    [Fact]
    public async Task PasaElUsuarioYElPlanAlWriter()
    {
        var plan = Plan();
        GivenOrder(Snapshot());
        GivenPlan(plan);
        GivenWriter();

        await CreateService().ConfirmAsync(5, usuarioId: 7);

        _writer.Verify(w => w.ApplyAsync(5, 7, plan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new OrderConfirmationService(null!, _reader.Object, _planner.Object, _writer.Object));
        Assert.Throws<ArgumentNullException>(
            () => new OrderConfirmationService(_tx, null!, _planner.Object, _writer.Object));
        Assert.Throws<ArgumentNullException>(
            () => new OrderConfirmationService(_tx, _reader.Object, null!, _writer.Object));
        Assert.Throws<ArgumentNullException>(
            () => new OrderConfirmationService(_tx, _reader.Object, _planner.Object, null!));
    }
}

using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using OrderSync.Tests.Inventario;
using Xunit;

namespace OrderSync.Tests.InventarioCrud;

public class StockMovementServiceTests
{
    private readonly PassThroughTransactionRunner _tx = new();
    private readonly Mock<IIngredientStore> _ingredients = new();
    private readonly Mock<IStockMovementStore> _movements = new();
    private readonly Mock<IStockAlertStore> _alerts = new();
    private readonly Mock<IMovementReader> _reader = new();
    private StockChange? _applied;

    public StockMovementServiceTests()
    {
        _movements.Setup(m => m.ApplyAsync(It.IsAny<StockChange>(), It.IsAny<CancellationToken>()))
                  .Callback<StockChange, CancellationToken>((c, _) => _applied = c)
                  .Returns(Task.CompletedTask);
    }

    private StockMovementService Create()
        => new(_tx, _ingredients.Object, _movements.Object, _alerts.Object, _reader.Object);

    private void GivenStock(decimal stock, decimal minimo)
        => _ingredients.Setup(i => i.LockAsync(1, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new IngredientStock(1, "Pan", "und", stock, minimo));

    private void VerifyNothingApplied()
        => _movements.Verify(m => m.ApplyAsync(It.IsAny<StockChange>(), It.IsAny<CancellationToken>()), Times.Never);

    // ---------- Mermas ----------

    [Fact]
    public async Task Merma_QueDejaElStockSobreElMinimo_DescuentaSinAlerta()
    {
        GivenStock(50m, 10m);

        var result = await Create().RegisterWasteAsync(9, new WasteRequest(1, 5m, "  Pan caído "));

        Assert.Equal(45m, result.StockActual);
        Assert.Equal(50m, result.StockAnterior);
        Assert.Equal(-5m, result.Delta);
        Assert.False(result.BajoMinimo);
        Assert.Equal("MERMA", _applied!.Tipo);
        Assert.Equal(-5m, _applied.Delta);
        Assert.False(_applied.AlertaAbierta);
        Assert.Equal(9, _applied.UsuarioId);
        Assert.Equal("Pan caído", _applied.Motivo);
    }

    [Fact]
    public async Task Merma_QueDejaElStockBajoElMinimo_AbreAlerta()
    {
        GivenStock(12m, 10m);

        var result = await Create().RegisterWasteAsync(9, new WasteRequest(1, 5m, "Vencido"));

        Assert.True(result.BajoMinimo);
        Assert.True(_applied!.AlertaAbierta);
        Assert.Equal(7m, _applied.NuevoStock);
    }

    [Fact]
    public async Task Merma_QueDejaElStockIgualAlMinimo_NoAbreAlerta()
    {
        GivenStock(15m, 10m);

        var result = await Create().RegisterWasteAsync(9, new WasteRequest(1, 5m, "Vencido"));

        Assert.False(result.BajoMinimo);
    }

    [Fact]
    public async Task Merma_MayorAlStock_LanzaReglaDeNegocio_YNoAplicaNada()
    {
        GivenStock(3m, 1m);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create().RegisterWasteAsync(9, new WasteRequest(1, 5m, "Vencido")));

        VerifyNothingApplied();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Merma_ConCantidadNoPositiva_LanzaValidacion(int cantidad)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().RegisterWasteAsync(9, new WasteRequest(1, cantidad, "Vencido")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Merma_SinMotivo_LanzaValidacion(string motivo)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().RegisterWasteAsync(9, new WasteRequest(1, 1m, motivo)));
    }

    [Fact]
    public async Task Merma_ConMotivoMuyLargo_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().RegisterWasteAsync(9, new WasteRequest(1, 1m, new string('x', 201))));
    }

    [Fact]
    public async Task Merma_IngredienteInexistente_LanzaNotFound()
    {
        _ingredients.Setup(i => i.LockAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((IngredientStock?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().RegisterWasteAsync(9, new WasteRequest(1, 1m, "Vencido")));
    }

    // ---------- Ajustes ----------

    [Fact]
    public async Task Ajuste_AlAlza_SumaLaDiferencia_YNoAbreAlerta()
    {
        GivenStock(5m, 10m);

        var result = await Create().RegisterAdjustmentAsync(9, new AdjustmentRequest(1, 20m, "Conteo físico"));

        Assert.Equal(15m, result.Delta);
        Assert.Equal(20m, result.StockActual);
        Assert.Equal("AJUSTE", _applied!.Tipo);
        Assert.False(_applied.AlertaAbierta);   // supera el mínimo: la alerta abierta se cierra
    }

    [Fact]
    public async Task Ajuste_ALaBaja_QueQuedaBajoElMinimo_AbreAlerta()
    {
        GivenStock(20m, 10m);

        var result = await Create().RegisterAdjustmentAsync(9, new AdjustmentRequest(1, 8m, "Conteo físico"));

        Assert.Equal(-12m, result.Delta);
        Assert.True(_applied!.AlertaAbierta);
    }

    [Fact]
    public async Task Ajuste_ConElMismoStock_LanzaReglaDeNegocio_YNoAplicaNada()
    {
        GivenStock(20m, 10m);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create().RegisterAdjustmentAsync(9, new AdjustmentRequest(1, 20m, "Conteo físico")));

        VerifyNothingApplied();
    }

    [Fact]
    public async Task Ajuste_ConStockNegativo_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().RegisterAdjustmentAsync(9, new AdjustmentRequest(1, -1m, "Conteo físico")));
    }

    [Fact]
    public async Task Ajuste_SinMotivo_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().RegisterAdjustmentAsync(9, new AdjustmentRequest(1, 5m, "")));
    }

    [Fact]
    public async Task Solicitudes_Nulas_LanzanArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().RegisterWasteAsync(9, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().RegisterAdjustmentAsync(9, null!));
    }

    // ---------- Alertas ----------

    [Fact]
    public async Task AtenderAlerta_Inexistente_LanzaNotFound()
    {
        _alerts.Setup(a => a.GetStateAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().AttendAlertAsync(4));
    }

    [Fact]
    public async Task AtenderAlerta_YaAtendida_LanzaConflicto()
    {
        _alerts.Setup(a => a.GetStateAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync("ATENDIDA");

        await Assert.ThrowsAsync<ConflictException>(() => Create().AttendAlertAsync(4));

        _alerts.Verify(a => a.MarkAttendedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AtenderAlerta_Abierta_LaMarcaComoAtendida()
    {
        _alerts.Setup(a => a.GetStateAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync("ABIERTA");

        await Create().AttendAlertAsync(4);

        _alerts.Verify(a => a.MarkAttendedAsync(4, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Movimientos ----------

    [Fact]
    public async Task Movimientos_NormalizaElTipo()
    {
        _reader.Setup(r => r.ListAsync(It.IsAny<MovementFilter>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((IReadOnlyList<MovementDto>)[]);

        await Create().ListMovementsAsync(new MovementFilter(1, " merma ", null, null));

        _reader.Verify(r => r.ListAsync(It.Is<MovementFilter>(f => f.Tipo == "MERMA"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Movimientos_TipoInvalido_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ListMovementsAsync(new MovementFilter(null, "ROBO", null, null)));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 201)]
    public async Task Movimientos_PaginacionInvalida_LanzaValidacion(int pagina, int tamano)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ListMovementsAsync(new MovementFilter(null, null, null, null, pagina, tamano)));
    }

    [Fact]
    public async Task Movimientos_DesdePosteriorAHasta_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListMovementsAsync(
            new MovementFilter(null, null, new DateTime(2026, 5, 2), new DateTime(2026, 5, 1))));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new StockMovementService(null!, _ingredients.Object, _movements.Object, _alerts.Object, _reader.Object));
        Assert.Throws<ArgumentNullException>(() => new StockMovementService(_tx, null!, _movements.Object, _alerts.Object, _reader.Object));
        Assert.Throws<ArgumentNullException>(() => new StockMovementService(_tx, _ingredients.Object, null!, _alerts.Object, _reader.Object));
        Assert.Throws<ArgumentNullException>(() => new StockMovementService(_tx, _ingredients.Object, _movements.Object, null!, _reader.Object));
        Assert.Throws<ArgumentNullException>(() => new StockMovementService(_tx, _ingredients.Object, _movements.Object, _alerts.Object, null!));
    }
}

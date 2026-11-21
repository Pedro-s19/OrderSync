using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using OrderSync.Application.Proveedores;
using OrderSync.Tests.Inventario;
using OrderSync.Tests.Support;
using Xunit;

namespace OrderSync.Tests.Proveedores;

public class PurchaseOrderServiceTests
{
    private readonly Mock<IPurchaseOrderRepository> _orders = new();
    private readonly Mock<ISupplierStore> _suppliers = new();
    private readonly Mock<IIngredientStore> _ingredients = new();
    private readonly Mock<IStockMovementStore> _movements = new();
    private readonly PassThroughTransactionRunner _tx = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 9, 0, 0));
    private readonly List<StockChange> _applied = new();
    private ReceiptRecord? _receipt;
    private NewPurchaseOrderData? _created;

    public PurchaseOrderServiceTests()
    {
        _movements.Setup(m => m.ApplyAsync(It.IsAny<StockChange>(), It.IsAny<CancellationToken>()))
                  .Callback<StockChange, CancellationToken>((c, _) => _applied.Add(c))
                  .Returns(Task.CompletedTask);
        _orders.Setup(o => o.RegisterReceiptAsync(It.IsAny<ReceiptRecord>(), It.IsAny<CancellationToken>()))
               .Callback<ReceiptRecord, CancellationToken>((r, _) => _receipt = r)
               .Returns(Task.CompletedTask);
        _orders.Setup(o => o.CreateAsync(It.IsAny<NewPurchaseOrderData>(), It.IsAny<CancellationToken>()))
               .Callback<NewPurchaseOrderData, CancellationToken>((d, _) => _created = d)
               .ReturnsAsync(Detail("BORRADOR"));
    }

    private PurchaseOrderService Create()
        => new(_orders.Object, _suppliers.Object, _ingredients.Object, _movements.Object, _tx, _clock);

    private static PurchaseOrderDetail Detail(string estado)
        => new(7, 1, "Dist", null, estado, DateTime.UtcNow, null, 0m, [], []);

    private void GivenSupplier(bool activo = true)
        => _suppliers.Setup(s => s.GetAsync(1, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new SupplierDto(1, "Dist", "Ana", null, null, activo));

    private void GivenNoSupplier()
        => _suppliers.Setup(s => s.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((SupplierDto?)null);

    private void GivenIngredients(params IngredientDto[] ingredients)
        => _ingredients.Setup(i => i.GetManyAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync((IReadOnlyList<IngredientDto>)ingredients);

    private void GivenPrices(params (int Id, decimal Price)[] prices)
        => _suppliers.Setup(s => s.GetPriceMapAsync(1, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((IReadOnlyDictionary<int, decimal>)prices.ToDictionary(p => p.Id, p => p.Price));

    private void GivenHeader(string? estado, DateOnly? esperada = null)
        => _orders.Setup(o => o.LockAsync(7, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(estado is null ? null : new PurchaseOrderHeader(7, estado, esperada));

    private void GivenLines(params PurchaseLineState[] lines)
        => _orders.Setup(o => o.GetLinesAsync(7, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((IReadOnlyList<PurchaseLineState>)lines);

    private void GivenDetail(string estado)
        => _orders.Setup(o => o.GetAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(Detail(estado));

    private void GivenStock(int ingredienteId, decimal stock, decimal minimo)
        => _ingredients.Setup(i => i.LockAsync(ingredienteId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new IngredientStock(ingredienteId, $"Ing{ingredienteId}", "g", stock, minimo));

    private static IngredientDto Ing(int id, bool activo = true) => new(id, $"Ing{id}", "g", 10m, 1m, activo);

    private static PurchaseOrderRequest Order(params PurchaseLineInput[] lineas)
        => new(1, null, lineas);

    // ---------- Crear ----------

    [Fact]
    public async Task Crear_SinPrecio_UsaElDeLaListaDelProveedor()
    {
        GivenSupplier();
        GivenIngredients(Ing(5));
        GivenPrices((5, 40m));

        await Create().CreateAsync(9, Order(new PurchaseLineInput(5, 10m, null)));

        var linea = Assert.Single(_created!.Lineas);
        Assert.Equal(40m, linea.PrecioUnitario);
        Assert.Equal(10m, linea.Cantidad);
        Assert.Equal(9, _created.UsuarioId);
    }

    [Fact]
    public async Task Crear_ConPrecioIndicado_PrevaleceSobreLaLista()
    {
        GivenSupplier();
        GivenIngredients(Ing(5));
        GivenPrices((5, 40m));

        await Create().CreateAsync(9, Order(new PurchaseLineInput(5, 10m, 35m)));

        Assert.Equal(35m, Assert.Single(_created!.Lineas).PrecioUnitario);
    }

    [Fact]
    public async Task Crear_SumaLasLineasRepetidasDelMismoIngrediente()
    {
        GivenSupplier();
        GivenIngredients(Ing(5));
        GivenPrices((5, 40m));

        await Create().CreateAsync(9, Order(new PurchaseLineInput(5, 10m, null), new PurchaseLineInput(5, 5m, null)));

        Assert.Equal(15m, Assert.Single(_created!.Lineas).Cantidad);
    }

    [Fact]
    public async Task Crear_SinPrecioEnLaListaNiEnLaLinea_LanzaValidacion()
    {
        GivenSupplier();
        GivenIngredients(Ing(5));
        GivenPrices();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(9, Order(new PurchaseLineInput(5, 10m, null))));
    }

    [Fact]
    public async Task Crear_ProveedorInexistente_LanzaNotFound()
    {
        GivenNoSupplier();

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().CreateAsync(9, Order(new PurchaseLineInput(5, 1m, 10m))));
    }

    [Fact]
    public async Task Crear_ProveedorDesactivado_LanzaReglaDeNegocio()
    {
        GivenSupplier(activo: false);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create().CreateAsync(9, Order(new PurchaseLineInput(5, 1m, 10m))));
    }

    [Fact]
    public async Task Crear_IngredienteInexistente_LanzaNotFound()
    {
        GivenSupplier();
        GivenIngredients();
        GivenPrices();

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().CreateAsync(9, Order(new PurchaseLineInput(5, 1m, 10m))));
    }

    [Fact]
    public async Task Crear_IngredienteDesactivado_LanzaReglaDeNegocio()
    {
        GivenSupplier();
        GivenIngredients(Ing(5, activo: false));
        GivenPrices();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create().CreateAsync(9, Order(new PurchaseLineInput(5, 1m, 10m))));
    }

    [Fact]
    public async Task Crear_SinLineas_LanzaValidacion()
    {
        GivenSupplier();

        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(9, Order()));
    }

    [Theory]
    [InlineData(0.0, 10.0)]
    [InlineData(-1.0, 10.0)]
    [InlineData(5.0, 0.0)]
    [InlineData(5.0, -3.0)]
    public async Task Crear_ConCantidadOPrecioNoPositivos_LanzaValidacion(double cantidad, double precio)
    {
        GivenSupplier();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(9, Order(new PurchaseLineInput(5, (decimal)cantidad, (decimal)precio))));
    }

    [Fact]
    public async Task Crear_ConFechaEsperadaPasada_LanzaValidacion()
    {
        GivenSupplier();

        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(9,
            new PurchaseOrderRequest(1, new DateOnly(2026, 3, 9), [new PurchaseLineInput(5, 1m, 10m)])));
    }

    [Fact]
    public async Task Crear_ConFechaEsperadaDeHoy_Funciona()
    {
        GivenSupplier();
        GivenIngredients(Ing(5));
        GivenPrices();

        await Create().CreateAsync(9, new PurchaseOrderRequest(
            1, new DateOnly(2026, 3, 10), [new PurchaseLineInput(5, 1m, 10m)]));

        Assert.Equal(new DateOnly(2026, 3, 10), _created!.FechaEsperada);
    }

    [Fact]
    public async Task Crear_ConSolicitudNula_LanzaArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().CreateAsync(9, null!));
    }

    // ---------- Editar, enviar, cancelar ----------

    [Fact]
    public async Task Editar_EnBorrador_Reemplaza()
    {
        GivenHeader("BORRADOR");
        GivenSupplier();
        GivenIngredients(Ing(5));
        GivenPrices((5, 40m));
        GivenDetail("BORRADOR");

        await Create().UpdateAsync(7, Order(new PurchaseLineInput(5, 3m, null)));

        _orders.Verify(o => o.ReplaceAsync(7, It.Is<NewPurchaseOrderData>(d => d.Lineas.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Editar_EnviadaOInexistente_LanzaError()
    {
        GivenHeader("ENVIADA");
        await Assert.ThrowsAsync<ConflictException>(() => Create().UpdateAsync(7, Order(new PurchaseLineInput(5, 1m, 1m))));

        GivenHeader(null);
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().UpdateAsync(7, Order(new PurchaseLineInput(5, 1m, 1m))));
    }

    [Fact]
    public async Task Enviar_DesdeBorrador_PasaAENVIADA()
    {
        GivenHeader("BORRADOR");
        GivenDetail("BORRADOR");
        GivenSupplier();

        await Create().SendAsync(7);

        _orders.Verify(o => o.SetStateAsync(7, "ENVIADA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("ENVIADA")]
    [InlineData("RECIBIDA")]
    [InlineData("CANCELADA")]
    public async Task Enviar_DesdeOtroEstado_LanzaConflicto(string estado)
    {
        GivenHeader(estado);

        await Assert.ThrowsAsync<ConflictException>(() => Create().SendAsync(7));
    }

    [Fact]
    public async Task Enviar_ConProveedorDesactivado_LanzaReglaDeNegocio()
    {
        GivenHeader("BORRADOR");
        GivenDetail("BORRADOR");
        GivenSupplier(activo: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().SendAsync(7));

        _orders.Verify(o => o.SetStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_Borrador_Funciona()
    {
        GivenHeader("BORRADOR");
        GivenDetail("CANCELADA");

        await Create().CancelAsync(7);

        _orders.Verify(o => o.SetStateAsync(7, "CANCELADA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_EnviadaSinRecepciones_Funciona()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 20m, 0m));
        GivenDetail("CANCELADA");

        await Create().CancelAsync(7);

        _orders.Verify(o => o.SetStateAsync(7, "CANCELADA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_EnviadaConMercanciaRecibida_LanzaConflicto()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 20m, 5m));

        await Assert.ThrowsAsync<ConflictException>(() => Create().CancelAsync(7));
    }

    [Theory]
    [InlineData("RECIBIDA")]
    [InlineData("CANCELADA")]
    public async Task Cancelar_DesdeEstadoFinal_LanzaConflicto(string estado)
    {
        GivenHeader(estado);

        await Assert.ThrowsAsync<ConflictException>(() => Create().CancelAsync(7));
    }

    // ---------- Recepción de mercancía ----------

    [Fact]
    public async Task Recibir_Parcial_SumaElStock_YLaOrdenSigueEnviada()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 20m, 0m));
        GivenStock(5, 50m, 10m);

        var result = await Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 8m)], "  Caja abierta "));

        Assert.Equal("ENVIADA", result.Estado);
        var ingreso = Assert.Single(result.Ingresos);
        Assert.Equal(50m, ingreso.StockAnterior);
        Assert.Equal(58m, ingreso.StockActual);
        var cambio = Assert.Single(_applied);
        Assert.Equal("RECEPCION", cambio.Tipo);
        Assert.Equal(8m, cambio.Delta);
        Assert.Equal(58m, cambio.NuevoStock);
        Assert.Equal(9, cambio.UsuarioId);
        Assert.False(_receipt!.OrdenCompleta);
        Assert.Equal("Caja abierta", _receipt.Observaciones);
    }

    [Fact]
    public async Task Recibir_LoPendienteCompleto_CierraLaOrden()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 20m, 12m));
        GivenStock(5, 50m, 10m);

        var result = await Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 8m)], null));

        Assert.Equal("RECIBIDA", result.Estado);
        Assert.True(_receipt!.OrdenCompleta);
    }

    [Fact]
    public async Task Recibir_QuedandoLineasPendientes_NoCierraLaOrden()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 20m, 0m), new PurchaseLineState(101, 9, 10m, 0m));
        GivenStock(5, 50m, 10m);

        var result = await Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 20m)], null));

        Assert.Equal("ENVIADA", result.Estado);
        Assert.False(_receipt!.OrdenCompleta);
    }

    [Fact]
    public async Task Recibir_MasDeLoPendiente_LanzaReglaDeNegocio_YNoAplicaNada()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 20m, 15m));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 6m)], null)));

        Assert.Empty(_applied);
        _orders.Verify(o => o.RegisterReceiptAsync(It.IsAny<ReceiptRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Recibir_LineaQueNoPerteneceALaOrden_LanzaValidacion()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 20m, 0m));

        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(999, 1m)], null)));
    }

    [Theory]
    [InlineData("BORRADOR")]
    [InlineData("RECIBIDA")]
    [InlineData("CANCELADA")]
    public async Task Recibir_SiLaOrdenNoEstaEnviada_LanzaConflicto(string estado)
    {
        GivenHeader(estado);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 1m)], null)));
    }

    [Fact]
    public async Task Recibir_OrdenInexistente_LanzaNotFound()
    {
        GivenHeader(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 1m)], null)));
    }

    [Fact]
    public async Task Recibir_SinLineas_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ReceiveAsync(9, 7, new ReceiptRequest([], null)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    public async Task Recibir_ConCantidadNoPositiva_LanzaValidacion(double cantidad)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, (decimal)cantidad)], null)));
    }

    [Fact]
    public async Task Recibir_LineaRepetida_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ReceiveAsync(9, 7,
            new ReceiptRequest([new ReceiptLineInput(100, 1m), new ReceiptLineInput(100, 2m)], null)));
    }

    [Fact]
    public async Task Recibir_AplicaLosIngredientesEnOrdenDeId_YAgrupaLosRepetidos()
    {
        GivenHeader("ENVIADA");
        GivenLines(
            new PurchaseLineState(100, 9, 10m, 0m),
            new PurchaseLineState(101, 5, 10m, 0m),
            new PurchaseLineState(102, 5, 10m, 0m));
        GivenStock(5, 0m, 0m);
        GivenStock(9, 0m, 0m);

        await Create().ReceiveAsync(9, 7, new ReceiptRequest(
            [new ReceiptLineInput(100, 4m), new ReceiptLineInput(101, 3m), new ReceiptLineInput(102, 2m)], null));

        Assert.Equal(new[] { 5, 9 }, _applied.Select(c => c.IngredienteId));
        Assert.Equal(5m, _applied[0].Delta);   // 3 + 2 del ingrediente 5
        Assert.Equal(4m, _applied[1].Delta);
    }

    [Fact]
    public async Task Recibir_SiElStockSigueBajoElMinimo_MantieneLaAlerta()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 100m, 0m));
        GivenStock(5, 2m, 50m);

        var result = await Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 10m)], null));

        Assert.True(Assert.Single(result.Ingresos).BajoMinimo);
        Assert.True(_applied[0].AlertaAbierta);
    }

    [Fact]
    public async Task Recibir_SiElStockSuperaElMinimo_CierraLaAlerta()
    {
        GivenHeader("ENVIADA");
        GivenLines(new PurchaseLineState(100, 5, 100m, 0m));
        GivenStock(5, 2m, 50m);

        var result = await Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 60m)], null));

        Assert.False(Assert.Single(result.Ingresos).BajoMinimo);
        Assert.False(_applied[0].AlertaAbierta);
    }

    [Theory]
    [InlineData(0, true)]    // la fecha esperada es hoy
    [InlineData(5, true)]    // llegó antes
    [InlineData(-1, false)]  // llegó tarde
    public async Task Recibir_MarcaSiLaEntregaFueATiempo(int diasHastaLaFechaEsperada, bool aTiempo)
    {
        var esperada = DateOnly.FromDateTime(_clock.LocalNow).AddDays(diasHastaLaFechaEsperada);
        GivenHeader("ENVIADA", esperada);
        GivenLines(new PurchaseLineState(100, 5, 20m, 0m));
        GivenStock(5, 0m, 0m);

        await Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 1m)], null));

        Assert.Equal(aTiempo, _receipt!.EntregaATiempo);
    }

    [Fact]
    public async Task Recibir_SinFechaEsperada_NoMarcaPuntualidad()
    {
        GivenHeader("ENVIADA", null);
        GivenLines(new PurchaseLineState(100, 5, 20m, 0m));
        GivenStock(5, 0m, 0m);

        await Create().ReceiveAsync(9, 7, new ReceiptRequest([new ReceiptLineInput(100, 1m)], null));

        Assert.Null(_receipt!.EntregaATiempo);
    }

    // ---------- Consultar ----------

    [Fact]
    public async Task Listar_NormalizaElEstado()
    {
        _orders.Setup(o => o.ListAsync(It.IsAny<PurchaseOrderFilter>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((IReadOnlyList<PurchaseOrderSummary>)[]);

        await Create().ListAsync(new PurchaseOrderFilter(" enviada ", null));

        _orders.Verify(o => o.ListAsync(It.Is<PurchaseOrderFilter>(f => f.Estado == "ENVIADA"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_ConEstadoOPaginacionInvalidos_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListAsync(new PurchaseOrderFilter("VOLANDO", null)));
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListAsync(new PurchaseOrderFilter(null, null, 0, 50)));
    }

    [Fact]
    public async Task Consultar_OrdenInexistente_LanzaNotFound()
    {
        _orders.Setup(o => o.GetAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((PurchaseOrderDetail?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(7));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PurchaseOrderService(null!, _suppliers.Object, _ingredients.Object, _movements.Object, _tx, _clock));
        Assert.Throws<ArgumentNullException>(() => new PurchaseOrderService(_orders.Object, null!, _ingredients.Object, _movements.Object, _tx, _clock));
        Assert.Throws<ArgumentNullException>(() => new PurchaseOrderService(_orders.Object, _suppliers.Object, null!, _movements.Object, _tx, _clock));
        Assert.Throws<ArgumentNullException>(() => new PurchaseOrderService(_orders.Object, _suppliers.Object, _ingredients.Object, null!, _tx, _clock));
        Assert.Throws<ArgumentNullException>(() => new PurchaseOrderService(_orders.Object, _suppliers.Object, _ingredients.Object, _movements.Object, null!, _clock));
        Assert.Throws<ArgumentNullException>(() => new PurchaseOrderService(_orders.Object, _suppliers.Object, _ingredients.Object, _movements.Object, _tx, null!));
    }
}

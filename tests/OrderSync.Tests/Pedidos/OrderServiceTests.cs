using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Inventario;
using OrderSync.Application.Pedidos;
using OrderSync.Tests.Inventario;
using Xunit;

namespace OrderSync.Tests.Pedidos;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IProductStore> _products = new();
    private readonly Mock<IInventoryRestorer> _restorer = new();
    private readonly PassThroughTransactionRunner _tx = new();

    private OrderService Create() => new(_orders.Object, _products.Object, _restorer.Object, _tx);

    private static ProductInfo Prod(int id, decimal precio = 18000m, bool activo = true)
        => new(id, $"Producto {id}", "Hamburguesas", precio, activo);

    private static OrderDetail Detail(string estado = "BORRADOR")
        => new(5, null, null, "MOSTRADOR", estado, null, null, null, 0m, DateTime.UtcNow, null, null, []);

    private static OrderRequest Mostrador(params OrderLineInput[] lineas)
        => new("MOSTRADOR", null, null, null, null, lineas);

    private void GivenProducts(params ProductInfo[] products)
        => _products.Setup(p => p.GetManyAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyList<ProductInfo>)products);

    private void GivenHeader(string? estado)
        => _orders.Setup(o => o.LockAsync(5, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(estado is null ? null : new OrderHeader(5, estado));

    private void GivenDetail(string estado)
        => _orders.Setup(o => o.GetAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(Detail(estado));

    private void GivenCreateWorks()
        => _orders.Setup(o => o.CreateAsync(It.IsAny<NewOrderData>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Detail());

    // ---------- Crear ----------

    [Fact]
    public async Task Crear_SumaLineasRepetidas_YUsaLosPreciosDelMenu()
    {
        NewOrderData? captured = null;
        GivenProducts(Prod(1, 18000m), Prod(2, 5000m));
        _orders.Setup(o => o.CreateAsync(It.IsAny<NewOrderData>(), It.IsAny<CancellationToken>()))
               .Callback<NewOrderData, CancellationToken>((d, _) => captured = d)
               .ReturnsAsync(Detail());

        await Create().CreateAsync(9, new OrderRequest(" mostrador ", null, null, null, null,
            [new OrderLineInput(1, 1), new OrderLineInput(2, 2), new OrderLineInput(1, 1)]));

        Assert.NotNull(captured);
        Assert.Equal("MOSTRADOR", captured!.Canal);
        Assert.Equal(9, captured.UsuarioId);
        Assert.Equal(2, captured.Lineas.Count);
        Assert.Equal(2, captured.Lineas.Single(l => l.ProductoId == 1).Cantidad);
        Assert.Equal(18000m, captured.Lineas.Single(l => l.ProductoId == 1).PrecioUnitario);
        Assert.Equal((2 * 18000m) + (2 * 5000m), captured.Total);
    }

    [Theory]
    [InlineData(null, "3001234567")]
    [InlineData("Calle 1 # 2-3", null)]
    [InlineData(null, null)]
    public async Task Domicilio_SinDireccionOTelefono_LanzaValidacion(string? direccion, string? telefono)
    {
        GivenProducts(Prod(1));

        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(9,
            new OrderRequest("DOMICILIO", null, direccion, telefono, null, [new OrderLineInput(1, 1)])));
    }

    [Fact]
    public async Task Domicilio_ConDireccionYTelefono_SeCrea()
    {
        GivenProducts(Prod(1));
        GivenCreateWorks();

        var result = await Create().CreateAsync(9, new OrderRequest(
            "DOMICILIO", null, "Calle 1 # 2-3", "3001234567", null, [new OrderLineInput(1, 1)]));

        Assert.Equal(5, result.PedidoId);
    }

    [Fact]
    public async Task Qr_SinMesa_LanzaValidacion()
    {
        GivenProducts(Prod(1));

        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(9,
            new OrderRequest("QR", null, null, null, null, [new OrderLineInput(1, 1)])));
    }

    [Fact]
    public async Task Qr_ConMesa_SeCrea()
    {
        GivenProducts(Prod(1));
        GivenCreateWorks();

        var result = await Create().CreateAsync(null, new OrderRequest(
            "QR", "M-4", null, null, null, [new OrderLineInput(1, 1)]));

        Assert.Equal(5, result.PedidoId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("DRIVE")]
    [InlineData(null)]
    public async Task CanalInvalido_LanzaValidacion(string? canal)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(9,
            new OrderRequest(canal!, null, null, null, null, [new OrderLineInput(1, 1)])));
    }

    [Fact]
    public async Task SinLineas_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(9, Mostrador()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1001)]
    public async Task CantidadFueraDeRango_LanzaValidacion(int cantidad)
    {
        GivenProducts(Prod(1));

        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(9, Mostrador(new OrderLineInput(1, cantidad))));
    }

    [Fact]
    public async Task MesaDemasiadoLarga_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(9,
            new OrderRequest("MOSTRADOR", "mesa-demasiado-larga", null, null, null, [new OrderLineInput(1, 1)])));
    }

    [Fact]
    public async Task ProductoInexistente_LanzaNotFound()
    {
        GivenProducts();

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().CreateAsync(9, Mostrador(new OrderLineInput(77, 1))));
    }

    [Fact]
    public async Task ProductoDesactivado_LanzaReglaDeNegocio()
    {
        GivenProducts(Prod(1, activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create().CreateAsync(9, Mostrador(new OrderLineInput(1, 1))));
    }

    [Fact]
    public async Task ClienteInactivo_LanzaReglaDeNegocio()
    {
        GivenProducts(Prod(1));
        _orders.Setup(o => o.CustomerIsActiveAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().CreateAsync(9,
            new OrderRequest("MOSTRADOR", null, null, null, 3, [new OrderLineInput(1, 1)])));
    }

    [Fact]
    public async Task SolicitudNula_LanzaArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().CreateAsync(9, null!));
    }

    // ---------- Editar ----------

    [Fact]
    public async Task Editar_PedidoEnBorrador_Reemplaza()
    {
        GivenHeader("BORRADOR");
        GivenProducts(Prod(1));
        GivenDetail("BORRADOR");

        var result = await Create().UpdateAsync(5, Mostrador(new OrderLineInput(1, 2)));

        Assert.Equal(5, result.PedidoId);
        _orders.Verify(o => o.ReplaceAsync(5, It.Is<NewOrderData>(d => d.Total == 36000m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Editar_PedidoConfirmado_LanzaConflicto_YNoReemplaza()
    {
        GivenHeader("CONFIRMADO");

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().UpdateAsync(5, Mostrador(new OrderLineInput(1, 1))));

        _orders.Verify(o => o.ReplaceAsync(It.IsAny<int>(), It.IsAny<NewOrderData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Editar_PedidoInexistente_LanzaNotFound()
    {
        GivenHeader(null);

        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => Create().UpdateAsync(5, Mostrador(new OrderLineInput(1, 1))));
    }

    // ---------- Marcar listo y entregar ----------

    [Theory]
    [InlineData("CONFIRMADO")]
    [InlineData("EN_COCINA")]
    public async Task MarcarListo_DesdeConfirmadoOEnCocina_PasaALISTO(string estado)
    {
        GivenHeader(estado);
        GivenDetail("LISTO");

        var result = await Create().MarkReadyAsync(5);

        Assert.Equal("LISTO", result.Estado);
        _orders.Verify(o => o.SetStateAsync(5, "LISTO", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("BORRADOR")]
    [InlineData("LISTO")]
    [InlineData("ENTREGADO")]
    [InlineData("CANCELADO")]
    public async Task MarcarListo_DesdeOtroEstado_LanzaConflicto(string estado)
    {
        GivenHeader(estado);

        await Assert.ThrowsAsync<ConflictException>(() => Create().MarkReadyAsync(5));

        _orders.Verify(o => o.SetStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarcarListo_PedidoInexistente_LanzaNotFound()
    {
        GivenHeader(null);

        await Assert.ThrowsAsync<OrderNotFoundException>(() => Create().MarkReadyAsync(5));
    }

    [Fact]
    public async Task Entregar_DesdeListo_PasaAENTREGADO()
    {
        GivenHeader("LISTO");
        GivenDetail("ENTREGADO");

        await Create().DeliverAsync(5);

        _orders.Verify(o => o.SetStateAsync(5, "ENTREGADO", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("BORRADOR")]
    [InlineData("CONFIRMADO")]
    [InlineData("ENTREGADO")]
    public async Task Entregar_DesdeOtroEstado_LanzaConflicto(string estado)
    {
        GivenHeader(estado);

        await Assert.ThrowsAsync<ConflictException>(() => Create().DeliverAsync(5));
    }

    // ---------- Cancelar ----------

    [Fact]
    public async Task Cancelar_Borrador_NoTocaElInventario()
    {
        GivenHeader("BORRADOR");

        var result = await Create().CancelAsync(5, 9);

        Assert.Equal("CANCELADO", result.Estado);
        Assert.Empty(result.Restituidos);
        _restorer.Verify(r => r.RestoreAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(o => o.SetStateAsync(5, "CANCELADO", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_Confirmado_DevuelveLosIngredientesAlInventario()
    {
        GivenHeader("CONFIRMADO");
        _restorer.Setup(r => r.RestoreAsync(5, 9, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((IReadOnlyList<RestoredIngredient>)[new RestoredIngredient(1, "Pan", 2m)]);

        var result = await Create().CancelAsync(5, 9);

        Assert.Equal("CANCELADO", result.Estado);
        var restituido = Assert.Single(result.Restituidos);
        Assert.Equal("Pan", restituido.Nombre);
        Assert.Equal(2m, restituido.Cantidad);
        _restorer.Verify(r => r.RestoreAsync(5, 9, It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(o => o.SetStateAsync(5, "CANCELADO", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("EN_COCINA")]
    [InlineData("LISTO")]
    [InlineData("ENTREGADO")]
    [InlineData("CANCELADO")]
    public async Task Cancelar_DesdeOtroEstado_LanzaConflicto_SinTocarNada(string estado)
    {
        GivenHeader(estado);

        await Assert.ThrowsAsync<ConflictException>(() => Create().CancelAsync(5, 9));

        _restorer.Verify(r => r.RestoreAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(o => o.SetStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelar_PedidoInexistente_LanzaNotFound()
    {
        GivenHeader(null);

        await Assert.ThrowsAsync<OrderNotFoundException>(() => Create().CancelAsync(5, 9));
    }

    // ---------- Consultar ----------

    [Fact]
    public async Task Listar_NormalizaEstadoYCanal_AntesDeConsultar()
    {
        _orders.Setup(o => o.ListAsync(It.IsAny<OrderFilter>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((IReadOnlyList<OrderSummary>)[]);

        await Create().ListAsync(new OrderFilter(" confirmado ", "qr", null, null));

        _orders.Verify(o => o.ListAsync(
            It.Is<OrderFilter>(f => f.Estado == "CONFIRMADO" && f.Canal == "QR"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_EstadoInvalido_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ListAsync(new OrderFilter("VOLANDO", null, null, null)));
    }

    [Fact]
    public async Task Listar_CanalInvalido_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ListAsync(new OrderFilter(null, "DRIVE", null, null)));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task Listar_PaginacionInvalida_LanzaValidacion(int pagina, int tamano)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ListAsync(new OrderFilter(null, null, null, null, pagina, tamano)));
    }

    [Fact]
    public async Task Listar_DesdePosteriorAHasta_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListAsync(
            new OrderFilter(null, null, new DateTime(2026, 5, 2), new DateTime(2026, 5, 1))));
    }

    [Fact]
    public async Task Consultar_PedidoInexistente_LanzaNotFound()
    {
        _orders.Setup(o => o.GetAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((OrderDetail?)null);

        await Assert.ThrowsAsync<OrderNotFoundException>(() => Create().GetAsync(9));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new OrderService(null!, _products.Object, _restorer.Object, _tx));
        Assert.Throws<ArgumentNullException>(() => new OrderService(_orders.Object, null!, _restorer.Object, _tx));
        Assert.Throws<ArgumentNullException>(() => new OrderService(_orders.Object, _products.Object, null!, _tx));
        Assert.Throws<ArgumentNullException>(() => new OrderService(_orders.Object, _products.Object, _restorer.Object, null!));
    }
}

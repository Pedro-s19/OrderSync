using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Crm;
using OrderSync.Application.Facturacion;
using OrderSync.Application.Inventario;
using OrderSync.Application.Pedidos;
using OrderSync.Tests.Inventario;
using OrderSync.Tests.Support;
using Xunit;

namespace OrderSync.Tests.Facturacion;

public class BillingServiceTests
{
    private readonly PassThroughTransactionRunner _tx = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IInvoiceStore> _invoices = new();
    private readonly Mock<ICustomerStore> _customers = new();
    private readonly Mock<IPromotionStore> _promotions = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 9, 0, 0));
    private NewInvoiceData? _created;

    public BillingServiceTests()
    {
        _orders.Setup(o => o.LockAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new OrderHeader(5, "ENTREGADO"));
        _invoices.Setup(i => i.ExistsForOrderAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _invoices.Setup(i => i.CreateAsync(It.IsAny<NewInvoiceData>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((NewInvoiceData d, CancellationToken _) =>
                 {
                     _created = d;
                     return new InvoiceDto(1, d.PedidoId, DateTime.UtcNow, d.Subtotal,
                         d.Subtotal + d.Propina - d.Total, d.Propina, d.Total, []);
                 });
    }

    private BillingService Create() => new(_tx, _orders.Object, _invoices.Object, _customers.Object, _promotions.Object, _clock);

    private void GivenOrder(decimal total, int? clienteId = null, string estado = "ENTREGADO")
    {
        _orders.Setup(o => o.LockAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new OrderHeader(5, estado));
        _orders.Setup(o => o.GetAsync(5, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new OrderDetail(5, clienteId, null, "MOSTRADOR", estado, null, null, null,
                   total, DateTime.UtcNow, null, null, []));
    }

    private void GivenCustomer(int puntos = 0, bool activo = true)
        => _customers.Setup(c => c.LockAsync(3, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new CustomerDto(3, "Luis", null, null, null, puntos, DateTime.UtcNow, activo));

    private void GivenPromotions(params PromotionDto[] promotions)
        => _promotions.Setup(p => p.GetAssignedAsync(3, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((IReadOnlyList<PromotionDto>)promotions);

    private static PromotionDto Promo(decimal pct = 10m, int? minimo = null)
        => new(2, "Cumpleaños", pct, minimo, new DateOnly(2026, 1, 1), null, true);

    private static ChargeRequest Charge(decimal propina, params PaymentInput[] pagos) => new(5, propina, pagos);

    private void VerifyNoInvoice()
        => _invoices.Verify(i => i.CreateAsync(It.IsAny<NewInvoiceData>(), It.IsAny<CancellationToken>()), Times.Never);

    // ---------- Cobro sin cliente ----------

    [Fact]
    public async Task Cobro_ConPagoExacto_CreaLaFactura()
    {
        GivenOrder(36_000m);

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 36_000m)));

        Assert.Equal(36_000m, result.Factura.Total);
        Assert.Equal(0m, result.Factura.Descuento);
        Assert.Equal(0, result.PuntosGanados);
        Assert.Null(result.Promocion);
        Assert.Equal(5, _created!.PedidoId);
    }

    [Fact]
    public async Task Cobro_ConPropina_LaMantieneAparteDelSubtotal()
    {
        GivenOrder(36_000m);

        var result = await Create().ChargeAsync(Charge(4_000m, new PaymentInput("TARJETA", 40_000m)));

        Assert.Equal(36_000m, result.Factura.Subtotal);
        Assert.Equal(4_000m, result.Factura.Propina);
        Assert.Equal(40_000m, result.Factura.Total);
    }

    [Fact]
    public async Task Cobro_ConPagosMixtos_LosGuardaTodos()
    {
        GivenOrder(36_000m);

        await Create().ChargeAsync(Charge(0m, new PaymentInput("efectivo", 20_000m), new PaymentInput("Digital", 16_000m)));

        Assert.Equal(new[] { "EFECTIVO", "DIGITAL" }, _created!.Pagos.Select(p => p.Metodo));
    }

    [Fact]
    public async Task Cobro_ConPagosQueNoSumanElTotal_LanzaReglaDeNegocio_YNoFactura()
    {
        GivenOrder(36_000m);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 30_000m))));

        VerifyNoInvoice();
    }

    [Fact]
    public async Task Cobro_SinPropinaIndicada_LaTomaComoCero()
    {
        GivenOrder(10_000m);

        var result = await Create().ChargeAsync(new ChargeRequest(5, null, [new PaymentInput("EFECTIVO", 10_000m)]));

        Assert.Equal(0m, result.Factura.Propina);
    }

    // ---------- Promoción y puntos ----------

    [Fact]
    public async Task Cobro_ConPromocionDelDiezPorCiento_AplicaElDescuento_YSumaPuntos()
    {
        GivenOrder(40_000m, clienteId: 3);
        GivenCustomer(puntos: 0);
        GivenPromotions(Promo(10m));

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 36_000m)));

        Assert.Equal("Cumpleaños", result.Promocion);
        Assert.Equal(10m, result.PorcentajeDescuento);
        Assert.Equal(40_000m, result.Factura.Subtotal);
        Assert.Equal(4_000m, result.Factura.Descuento);
        Assert.Equal(36_000m, result.Factura.Total);
        Assert.Equal(36, result.PuntosGanados);
        _customers.Verify(c => c.AddPointsAsync(3, 5, 36, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cobro_ConClienteSinPromocion_SumaPuntosSobreElSubtotal()
    {
        GivenOrder(25_500m, clienteId: 3);
        GivenCustomer();
        GivenPromotions();

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 25_500m)));

        Assert.Equal(25, result.PuntosGanados);   // 1 punto por cada $1.000, redondeado hacia abajo
    }

    [Fact]
    public async Task Cobro_LaPropinaNoSumaPuntos()
    {
        GivenOrder(10_000m, clienteId: 3);
        GivenCustomer();
        GivenPromotions();

        var result = await Create().ChargeAsync(Charge(5_000m, new PaymentInput("EFECTIVO", 15_000m)));

        Assert.Equal(10, result.PuntosGanados);
    }

    [Fact]
    public async Task Cobro_PromocionConPuntosMinimosNoCumplidos_NoSeAplica()
    {
        GivenOrder(40_000m, clienteId: 3);
        GivenCustomer(puntos: 10);
        GivenPromotions(Promo(10m, minimo: 100));

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 40_000m)));

        Assert.Null(result.Promocion);
        Assert.Equal(0m, result.Factura.Descuento);
    }

    [Fact]
    public async Task Cobro_PromocionConPuntosMinimosCumplidos_SeAplica()
    {
        GivenOrder(40_000m, clienteId: 3);
        GivenCustomer(puntos: 100);
        GivenPromotions(Promo(10m, minimo: 100));

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 36_000m)));

        Assert.Equal("Cumpleaños", result.Promocion);
    }

    [Fact]
    public async Task Cobro_ConClienteDesactivado_NoAplicaDescuentoNiSumaPuntos()
    {
        GivenOrder(40_000m, clienteId: 3);
        GivenCustomer(activo: false);
        GivenPromotions(Promo(10m));

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 40_000m)));

        Assert.Equal(0m, result.Factura.Descuento);
        Assert.Equal(0, result.PuntosGanados);
        _customers.Verify(c => c.AddPointsAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cobro_DeMenosDeMilPesos_NoSumaPuntos()
    {
        GivenOrder(900m, clienteId: 3);
        GivenCustomer();
        GivenPromotions();

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 900m)));

        Assert.Equal(0, result.PuntosGanados);
        _customers.Verify(c => c.AddPointsAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cobro_RedondeaElDescuentoADosDecimales_HaciaArribaEnLaMitad()
    {
        GivenOrder(10_001m, clienteId: 3);
        GivenCustomer();
        GivenPromotions(Promo(12.5m));   // 10.001 x 12,5 % = 1.250,125 -> 1.250,13

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 8_750.87m)));

        Assert.Equal(1_250.13m, result.Factura.Descuento);
        Assert.Equal(8_750.87m, result.Factura.Total);
    }

    // ---------- Estado del pedido ----------

    [Theory]
    [InlineData("BORRADOR")]
    [InlineData("CANCELADO")]
    public async Task Cobro_DePedidoQueNoSePuedeCobrar_LanzaConflicto(string estado)
    {
        GivenOrder(36_000m, estado: estado);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 36_000m))));

        VerifyNoInvoice();
    }

    [Theory]
    [InlineData("CONFIRMADO")]
    [InlineData("EN_COCINA")]
    [InlineData("LISTO")]
    [InlineData("ENTREGADO")]
    public async Task Cobro_DePedidoConfirmadoEnAdelante_Funciona(string estado)
    {
        GivenOrder(36_000m, estado: estado);

        var result = await Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 36_000m)));

        Assert.Equal(1, result.Factura.FacturaId);
    }

    [Fact]
    public async Task Cobro_DePedidoYaFacturado_LanzaConflicto()
    {
        GivenOrder(36_000m);
        _invoices.Setup(i => i.ExistsForOrderAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 36_000m))));

        VerifyNoInvoice();
    }

    [Fact]
    public async Task Cobro_DePedidoInexistente_LanzaNotFound()
    {
        _orders.Setup(o => o.LockAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((OrderHeader?)null);

        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", 1m))));
    }

    // ---------- Validación de la solicitud ----------

    [Fact]
    public async Task Cobro_ConPropinaNegativa_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ChargeAsync(Charge(-1m, new PaymentInput("EFECTIVO", 1m))));
    }

    [Fact]
    public async Task Cobro_SinPagos_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ChargeAsync(Charge(0m)));
    }

    [Theory]
    [InlineData("CHEQUE")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Cobro_ConMetodoInvalido_LanzaValidacion(string? metodo)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ChargeAsync(Charge(0m, new PaymentInput(metodo!, 1m))));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public async Task Cobro_ConMontoNoPositivo_LanzaValidacion(double monto)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ChargeAsync(Charge(0m, new PaymentInput("EFECTIVO", (decimal)monto))));
    }

    [Fact]
    public async Task Cobro_ConSolicitudNula_LanzaArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().ChargeAsync(null!));
    }

    // ---------- Consulta ----------

    [Fact]
    public async Task Consultar_FacturaInexistente_LanzaNotFound()
    {
        _invoices.Setup(i => i.GetAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((InvoiceDto?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(9));
    }

    [Fact]
    public async Task Consultar_PedidoSinFactura_LanzaNotFound()
    {
        _invoices.Setup(i => i.GetByOrderAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((InvoiceDto?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetByOrderAsync(9));
    }

    [Fact]
    public async Task Listar_ConvierteLasFechasLocalesAUtc()
    {
        _invoices.Setup(i => i.ListAsync(It.IsAny<InvoiceQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((IReadOnlyList<InvoiceSummary>)[]);

        await Create().ListAsync(new InvoiceFilter(new DateTime(2026, 3, 1, 15, 30, 0), new DateTime(2026, 3, 2, 8, 0, 0)));

        _invoices.Verify(i => i.ListAsync(It.Is<InvoiceQuery>(q =>
            q.DesdeUtc == new DateTime(2026, 3, 1, 5, 0, 0) &&
            q.HastaUtcExclusivo == new DateTime(2026, 3, 3, 5, 0, 0)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_SinFechas_NoFiltraPorRango()
    {
        _invoices.Setup(i => i.ListAsync(It.IsAny<InvoiceQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((IReadOnlyList<InvoiceSummary>)[]);

        await Create().ListAsync(new InvoiceFilter(null, null));

        _invoices.Verify(i => i.ListAsync(It.Is<InvoiceQuery>(q => q.DesdeUtc == null && q.HastaUtcExclusivo == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_ConFiltrosInvalidos_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListAsync(new InvoiceFilter(null, null, 0, 50)));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ListAsync(new InvoiceFilter(new DateTime(2026, 3, 5), new DateTime(2026, 3, 1))));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new BillingService(null!, _orders.Object, _invoices.Object, _customers.Object, _promotions.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new BillingService(_tx, null!, _invoices.Object, _customers.Object, _promotions.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new BillingService(_tx, _orders.Object, null!, _customers.Object, _promotions.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new BillingService(_tx, _orders.Object, _invoices.Object, null!, _promotions.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new BillingService(_tx, _orders.Object, _invoices.Object, _customers.Object, null!, _clock));
        Assert.Throws<ArgumentNullException>(() => new BillingService(_tx, _orders.Object, _invoices.Object, _customers.Object, _promotions.Object, null!));
    }
}

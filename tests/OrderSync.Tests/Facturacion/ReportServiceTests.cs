using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Facturacion;
using OrderSync.Tests.Support;
using Xunit;

namespace OrderSync.Tests.Facturacion;

public class ReportServiceTests
{
    private readonly Mock<IReportReader> _reader = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 9, 0, 0));

    private ReportService Create() => new(_reader.Object, _clock);

    private void GivenInvoices(params InvoiceRow[] rows)
        => _reader.Setup(r => r.GetInvoiceRowsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((IReadOnlyList<InvoiceRow>)rows);

    private void GivenProducts(params ProductSalesRow[] rows)
        => _reader.Setup(r => r.GetProductSalesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((IReadOnlyList<ProductSalesRow>)rows);

    private static readonly DateTime Desde = new(2026, 3, 1);
    private static readonly DateTime Hasta = new(2026, 3, 2);

    // ---------- Ventas por día (FR-11) ----------

    [Fact]
    public async Task VentasPorDia_AgrupaPorDiaLocal_NoPorDiaUtc()
    {
        // 03:00 UTC del 2 de marzo = 22:00 del 1 de marzo en Colombia.
        GivenInvoices(
            new InvoiceRow(new DateTime(2026, 3, 2, 3, 0, 0), 10_000m, 0m, 10_000m),
            new InvoiceRow(new DateTime(2026, 3, 2, 15, 0, 0), 20_000m, 0m, 20_000m),
            new InvoiceRow(new DateTime(2026, 3, 2, 20, 0, 0), 30_000m, 0m, 30_000m));

        var report = await Create().DailySalesAsync(Desde, Hasta);

        Assert.Equal(2, report.Dias.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), report.Dias[0].Fecha);
        Assert.Equal(1, report.Dias[0].Facturas);
        Assert.Equal(new DateOnly(2026, 3, 2), report.Dias[1].Fecha);
        Assert.Equal(2, report.Dias[1].Facturas);
        Assert.Equal(50_000m, report.Dias[1].Total);
    }

    [Fact]
    public async Task VentasPorDia_CalculaDescuentosYPropinas()
    {
        // Subtotal 40.000, propina 4.000, total 40.000 => descuento 4.000.
        GivenInvoices(new InvoiceRow(new DateTime(2026, 3, 1, 17, 0, 0), 40_000m, 4_000m, 40_000m));

        var report = await Create().DailySalesAsync(Desde, Hasta);

        var dia = Assert.Single(report.Dias);
        Assert.Equal(40_000m, dia.Subtotal);
        Assert.Equal(4_000m, dia.Descuentos);
        Assert.Equal(4_000m, dia.Propinas);
        Assert.Equal(40_000m, dia.Total);
    }

    [Fact]
    public async Task VentasPorDia_SumaLosTotalesDelRango()
    {
        GivenInvoices(
            new InvoiceRow(new DateTime(2026, 3, 1, 17, 0, 0), 10_000m, 1_000m, 11_000m),
            new InvoiceRow(new DateTime(2026, 3, 2, 17, 0, 0), 20_000m, 2_000m, 22_000m));

        var report = await Create().DailySalesAsync(Desde, Hasta);

        Assert.Equal(2, report.TotalFacturas);
        Assert.Equal(33_000m, report.TotalVentas);
        Assert.Equal(3_000m, report.TotalPropinas);
        Assert.Equal(new DateOnly(2026, 3, 1), report.Desde);
        Assert.Equal(new DateOnly(2026, 3, 2), report.Hasta);
    }

    [Fact]
    public async Task VentasPorDia_SinVentas_DevuelveElReporteVacio()
    {
        GivenInvoices();

        var report = await Create().DailySalesAsync(Desde, Hasta);

        Assert.Empty(report.Dias);
        Assert.Equal(0m, report.TotalVentas);
    }

    [Fact]
    public async Task VentasPorDia_ConsultaElRangoLocalConvertidoAUtc()
    {
        GivenInvoices();

        await Create().DailySalesAsync(new DateTime(2026, 3, 1, 14, 0, 0), new DateTime(2026, 3, 2, 8, 0, 0));

        _reader.Verify(r => r.GetInvoiceRowsAsync(
            new DateTime(2026, 3, 1, 5, 0, 0), new DateTime(2026, 3, 3, 5, 0, 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VentasPorDia_ConDesdePosteriorAHasta_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().DailySalesAsync(Hasta, Desde));
    }

    [Fact]
    public async Task VentasPorDia_ConRangoMayorAUnAnio_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().DailySalesAsync(Desde, Desde.AddDays(366)));
    }

    [Fact]
    public async Task VentasPorDia_ConUnAnioExacto_EsValido()
    {
        GivenInvoices();

        var report = await Create().DailySalesAsync(Desde, Desde.AddDays(365));

        Assert.Empty(report.Dias);
    }

    // ---------- Productos más vendidos (FR-12) ----------

    [Fact]
    public async Task Productos_PorDefectoSeOrdenanPorCantidad()
    {
        GivenProducts(
            new ProductSalesRow(1, "Clásica", 10, 180_000m),
            new ProductSalesRow(2, "Doble", 25, 400_000m),
            new ProductSalesRow(3, "Papas", 40, 120_000m));

        var result = await Create().TopProductsAsync(Desde, Hasta, null, 10);

        Assert.Equal(new[] { "Papas", "Doble", "Clásica" }, result.Select(p => p.Producto));
    }

    [Fact]
    public async Task Productos_PorIngresos_CambiaElOrden()
    {
        GivenProducts(
            new ProductSalesRow(1, "Clásica", 10, 180_000m),
            new ProductSalesRow(2, "Doble", 25, 400_000m),
            new ProductSalesRow(3, "Papas", 40, 120_000m));

        var result = await Create().TopProductsAsync(Desde, Hasta, "INGRESOS", 10);

        Assert.Equal(new[] { "Doble", "Clásica", "Papas" }, result.Select(p => p.Producto));
    }

    [Fact]
    public async Task Productos_RespetaElTop()
    {
        GivenProducts(
            new ProductSalesRow(1, "A", 10, 1m), new ProductSalesRow(2, "B", 20, 1m), new ProductSalesRow(3, "C", 30, 1m));

        var result = await Create().TopProductsAsync(Desde, Hasta, "cantidad", 2);

        Assert.Equal(new[] { "C", "B" }, result.Select(p => p.Producto));
    }

    [Fact]
    public async Task Productos_ConCantidadIgual_DesempataPorIngresos()
    {
        GivenProducts(new ProductSalesRow(1, "Barato", 10, 100m), new ProductSalesRow(2, "Caro", 10, 900m));

        var result = await Create().TopProductsAsync(Desde, Hasta, "cantidad", 10);

        Assert.Equal("Caro", result[0].Producto);
    }

    [Fact]
    public async Task Productos_ConOrdenInvalido_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().TopProductsAsync(Desde, Hasta, "color", 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Productos_ConTopFueraDeRango_LanzaValidacion(int top)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().TopProductsAsync(Desde, Hasta, null, top));
    }

    [Fact]
    public async Task Productos_ConDesdePosteriorAHasta_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().TopProductsAsync(Hasta, Desde, null, 10));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ReportService(null!, _clock));
        Assert.Throws<ArgumentNullException>(() => new ReportService(_reader.Object, null!));
    }
}

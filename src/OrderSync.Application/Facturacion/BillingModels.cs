namespace OrderSync.Application.Facturacion;

public static class PaymentMethods
{
    public const string Efectivo = "EFECTIVO";
    public const string Tarjeta = "TARJETA";
    public const string Digital = "DIGITAL";

    public static readonly IReadOnlyList<string> All = [Efectivo, Tarjeta, Digital];
}

// ---- Cobro ----
public sealed record PaymentInput(string Metodo, decimal Monto);

public sealed record ChargeRequest(int PedidoId, decimal? Propina, IReadOnlyList<PaymentInput> Pagos);

public sealed record NewInvoiceData(
    int PedidoId, decimal Subtotal, decimal Propina, decimal Total, IReadOnlyList<PaymentInput> Pagos);

public sealed record PaymentDto(int PagoId, string Metodo, decimal Monto, DateTime Fecha);

/// <summary>Subtotal es el valor del pedido antes de descuento; Descuento = Subtotal + Propina - Total.</summary>
public sealed record InvoiceDto(
    int FacturaId, int PedidoId, DateTime Fecha, decimal Subtotal, decimal Descuento,
    decimal Propina, decimal Total, IReadOnlyList<PaymentDto> Pagos);

public sealed record ChargeResult(InvoiceDto Factura, string? Promocion, decimal PorcentajeDescuento, int PuntosGanados);

public sealed record InvoiceSummary(int FacturaId, int PedidoId, DateTime Fecha, decimal Total);

public sealed record InvoiceFilter(DateTime? Desde, DateTime? Hasta, int Pagina = 1, int TamanoPagina = 50);

/// <summary>Consulta ya convertida a UTC: [DesdeUtc, HastaUtcExclusivo).</summary>
public sealed record InvoiceQuery(DateTime? DesdeUtc, DateTime? HastaUtcExclusivo, int Pagina, int TamanoPagina);

// ---- Reportes ----
public sealed record InvoiceRow(DateTime FechaUtc, decimal Subtotal, decimal Propina, decimal Total);

public sealed record ProductSalesRow(int ProductoId, string Producto, int Cantidad, decimal Ingresos);

public sealed record DailySalesDto(
    DateOnly Fecha, int Facturas, decimal Subtotal, decimal Descuentos, decimal Propinas, decimal Total);

public sealed record DailySalesReport(
    DateOnly Desde, DateOnly Hasta, IReadOnlyList<DailySalesDto> Dias,
    int TotalFacturas, decimal TotalVentas, decimal TotalPropinas);

public sealed record ProductSalesDto(int ProductoId, string Producto, int Cantidad, decimal Ingresos);

// ---- Puertos ----
public interface IInvoiceStore
{
    Task<bool> ExistsForOrderAsync(int pedidoId, CancellationToken ct = default);
    Task<InvoiceDto> CreateAsync(NewInvoiceData invoice, CancellationToken ct = default);
    Task<InvoiceDto?> GetAsync(int facturaId, CancellationToken ct = default);
    Task<InvoiceDto?> GetByOrderAsync(int pedidoId, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceSummary>> ListAsync(InvoiceQuery query, CancellationToken ct = default);
}

public interface IReportReader
{
    Task<IReadOnlyList<InvoiceRow>> GetInvoiceRowsAsync(DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSalesRow>> GetProductSalesAsync(DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken ct = default);
}

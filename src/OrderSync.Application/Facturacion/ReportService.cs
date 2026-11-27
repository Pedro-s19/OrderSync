using OrderSync.Application.Common;

namespace OrderSync.Application.Facturacion;

/// <summary>Reportes de ventas: por día (FR-11) y productos más vendidos (FR-12).</summary>
public sealed class ReportService
{
    public const int MaxRangeDays = 366;

    private readonly IReportReader _reader;
    private readonly IClock _clock;

    public ReportService(IReportReader reader, IClock clock)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<DailySalesReport> DailySalesAsync(DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var (desdeUtc, hastaUtc) = ToUtcRange(desde, hasta);
        var filas = await _reader.GetInvoiceRowsAsync(desdeUtc, hastaUtc, ct);

        var dias = filas
            .GroupBy(f => DateOnly.FromDateTime(_clock.ToLocal(f.FechaUtc)))
            .OrderBy(g => g.Key)
            .Select(g => new DailySalesDto(
                g.Key,
                g.Count(),
                g.Sum(f => f.Subtotal),
                g.Sum(f => f.Subtotal + f.Propina - f.Total),
                g.Sum(f => f.Propina),
                g.Sum(f => f.Total)))
            .ToList();

        return new DailySalesReport(
            DateOnly.FromDateTime(desde), DateOnly.FromDateTime(hasta), dias,
            dias.Sum(d => d.Facturas), dias.Sum(d => d.Total), dias.Sum(d => d.Propinas));
    }

    /// <summary>Ordenado por "cantidad" o por "ingresos" (valor de las líneas vendidas, sin descuentos).</summary>
    public async Task<IReadOnlyList<ProductSalesDto>> TopProductsAsync(
        DateTime desde, DateTime hasta, string? orden, int top, CancellationToken ct = default)
    {
        var criterio = string.IsNullOrWhiteSpace(orden) ? "cantidad" : orden.Trim().ToLowerInvariant();
        if (criterio is not ("cantidad" or "ingresos"))
            throw new RequestValidationException("El orden debe ser 'cantidad' o 'ingresos'.");
        if (top < 1 || top > 100)
            throw new RequestValidationException("El top debe estar entre 1 y 100.");

        var (desdeUtc, hastaUtc) = ToUtcRange(desde, hasta);
        var filas = await _reader.GetProductSalesAsync(desdeUtc, hastaUtc, ct);

        var ordenadas = criterio == "cantidad"
            ? filas.OrderByDescending(f => f.Cantidad).ThenByDescending(f => f.Ingresos)
            : filas.OrderByDescending(f => f.Ingresos).ThenByDescending(f => f.Cantidad);

        return ordenadas.ThenBy(f => f.Producto).Take(top)
            .Select(f => new ProductSalesDto(f.ProductoId, f.Producto, f.Cantidad, f.Ingresos))
            .ToList();
    }

    private (DateTime DesdeUtc, DateTime HastaUtcExclusivo) ToUtcRange(DateTime desde, DateTime hasta)
    {
        if (desde.Date > hasta.Date)
            throw new RequestValidationException("La fecha 'desde' no puede ser posterior a 'hasta'.");
        if ((hasta.Date - desde.Date).TotalDays >= MaxRangeDays)
            throw new RequestValidationException($"El rango máximo es de {MaxRangeDays} días.");

        return (_clock.ToUtc(desde.Date), _clock.ToUtc(hasta.Date.AddDays(1)));
    }
}

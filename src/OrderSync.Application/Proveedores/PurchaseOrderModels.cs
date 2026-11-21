using OrderSync.Application.Inventario;

namespace OrderSync.Application.Proveedores;

public static class PurchaseOrderStatus
{
    public const string Borrador = "BORRADOR";
    public const string Enviada = "ENVIADA";
    public const string Recibida = "RECIBIDA";
    public const string Cancelada = "CANCELADA";

    public static readonly IReadOnlyList<string> All = [Borrador, Enviada, Recibida, Cancelada];
}

// ---- Entrada ----
public sealed record PurchaseLineInput(int IngredienteId, decimal Cantidad, decimal? PrecioUnitario);

public sealed record PurchaseOrderRequest(int ProveedorId, DateOnly? FechaEsperada, IReadOnlyList<PurchaseLineInput> Lineas);

public sealed record PurchaseOrderFilter(string? Estado, int? ProveedorId, int Pagina = 1, int TamanoPagina = 50);

public sealed record ReceiptLineInput(int DetalleId, decimal Cantidad);

public sealed record ReceiptRequest(IReadOnlyList<ReceiptLineInput> Lineas, string? Observaciones);

// ---- Interno ----
public sealed record PurchasePricedLine(int IngredienteId, decimal Cantidad, decimal PrecioUnitario);

public sealed record NewPurchaseOrderData(
    int ProveedorId, int? UsuarioId, DateOnly? FechaEsperada, IReadOnlyList<PurchasePricedLine> Lineas);

public sealed record PurchaseOrderHeader(int OrdenCompraId, string Estado, DateOnly? FechaEsperada);

public sealed record PurchaseLineState(int DetalleId, int IngredienteId, decimal Cantidad, decimal CantidadRecibida);

public sealed record ReceiptRecord(
    int OrdenCompraId, int? UsuarioId, bool? EntregaATiempo, string? Observaciones,
    IReadOnlyList<ReceiptLineInput> Lineas, bool OrdenCompleta);

// ---- Salida ----
public sealed record PurchaseLineDetail(
    int DetalleId, int IngredienteId, string Ingrediente, string UnidadMedida,
    decimal Cantidad, decimal PrecioUnitario, decimal CantidadRecibida, decimal Subtotal);

public sealed record ReceiptSummary(int RecepcionId, DateTime Fecha, int? UsuarioId, bool? EntregaATiempo, string? Observaciones);

public sealed record PurchaseOrderDetail(
    int OrdenCompraId, int ProveedorId, string Proveedor, int? UsuarioId, string Estado,
    DateTime FechaCreacion, DateOnly? FechaEsperada, decimal Total,
    IReadOnlyList<PurchaseLineDetail> Lineas, IReadOnlyList<ReceiptSummary> Recepciones);

public sealed record PurchaseOrderSummary(
    int OrdenCompraId, int ProveedorId, string Proveedor, string Estado,
    DateTime FechaCreacion, DateOnly? FechaEsperada, decimal Total);

public sealed record ReceiptResult(int OrdenCompraId, string Estado, IReadOnlyList<StockChangeResult> Ingresos);

public interface IPurchaseOrderRepository
{
    Task<IReadOnlyList<PurchaseOrderSummary>> ListAsync(PurchaseOrderFilter filter, CancellationToken ct = default);
    Task<PurchaseOrderDetail?> GetAsync(int ordenCompraId, CancellationToken ct = default);

    /// <summary>Bloquea la orden (dentro de una transacción) y devuelve su estado.</summary>
    Task<PurchaseOrderHeader?> LockAsync(int ordenCompraId, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseLineState>> GetLinesAsync(int ordenCompraId, CancellationToken ct = default);
    Task<PurchaseOrderDetail> CreateAsync(NewPurchaseOrderData order, CancellationToken ct = default);
    Task ReplaceAsync(int ordenCompraId, NewPurchaseOrderData order, CancellationToken ct = default);
    Task SetStateAsync(int ordenCompraId, string estado, CancellationToken ct = default);

    /// <summary>Guarda la recepción, suma lo recibido a cada línea y cierra la orden si quedó completa.</summary>
    Task RegisterReceiptAsync(ReceiptRecord receipt, CancellationToken ct = default);
}

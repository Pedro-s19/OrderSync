namespace OrderSync.Application.Pedidos;

public static class OrderStatus
{
    public const string Borrador = "BORRADOR";
    public const string Confirmado = "CONFIRMADO";
    public const string EnCocina = "EN_COCINA";
    public const string Listo = "LISTO";
    public const string Entregado = "ENTREGADO";
    public const string Cancelado = "CANCELADO";

    public static readonly IReadOnlyList<string> All =
        [Borrador, Confirmado, EnCocina, Listo, Entregado, Cancelado];
}

public static class OrderChannels
{
    public const string Mostrador = "MOSTRADOR";
    public const string Domicilio = "DOMICILIO";
    public const string Qr = "QR";

    public static readonly IReadOnlyList<string> All = [Mostrador, Domicilio, Qr];
}

// ---- Entrada ----
public sealed record OrderLineInput(int ProductoId, int Cantidad);

public sealed record OrderRequest(
    string Canal, string? Mesa, string? DireccionEntrega, string? TelefonoContacto,
    int? ClienteId, IReadOnlyList<OrderLineInput> Lineas);

public sealed record OrderFilter(
    string? Estado, string? Canal, DateTime? Desde, DateTime? Hasta, int Pagina = 1, int TamanoPagina = 50);

// ---- Interno (ya validado y con precios) ----
public sealed record PricedLine(int ProductoId, int Cantidad, decimal PrecioUnitario);

public sealed record NewOrderData(
    int? ClienteId, int? UsuarioId, string Canal, string? Mesa,
    string? DireccionEntrega, string? TelefonoContacto, decimal Total, IReadOnlyList<PricedLine> Lineas);

public sealed record OrderHeader(int PedidoId, string Estado);

// ---- Salida ----
public sealed record OrderLineDetail(
    int DetalleId, int ProductoId, string Producto, int Cantidad, decimal PrecioUnitario, decimal Subtotal);

public sealed record OrderDetail(
    int PedidoId, int? ClienteId, int? UsuarioId, string Canal, string Estado, string? Mesa,
    string? DireccionEntrega, string? TelefonoContacto, decimal Total, DateTime FechaCreacion,
    DateTime? FechaEnvioCocina, DateTime? FechaListo, IReadOnlyList<OrderLineDetail> Lineas);

public sealed record OrderSummary(
    int PedidoId, string Canal, string Estado, string? Mesa, decimal Total, DateTime FechaCreacion, int Items);

public sealed record RestoredIngredient(int IngredienteId, string Nombre, decimal Cantidad);

public sealed record OrderCancellationResult(int PedidoId, string Estado, IReadOnlyList<RestoredIngredient> Restituidos);

// ---- Productos ----
public sealed record ProductInfo(int ProductoId, string Nombre, string? Categoria, decimal Precio, bool Activo);

public sealed record ProductRequest(string Nombre, string? Categoria, decimal Precio);

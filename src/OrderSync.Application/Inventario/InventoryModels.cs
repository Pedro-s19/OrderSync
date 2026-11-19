namespace OrderSync.Application.Inventario;

public static class OrderStates
{
    public const string Borrador = "BORRADOR";
    public const string Confirmado = "CONFIRMADO";
}

public static class AlertStates
{
    public const string Abierta = "ABIERTA";
    public const string Atendida = "ATENDIDA";
}

// ---- Lo que se lee de la base ----
public sealed record OrderLine(int ProductoId, int Cantidad);

public sealed record RecipeItem(int ProductoId, int IngredienteId, decimal Cantidad);

public sealed record IngredientStock(
    int IngredienteId, string Nombre, string UnidadMedida, decimal StockActual, decimal StockMinimo);

public sealed record OrderInventorySnapshot(
    int PedidoId,
    string Estado,
    IReadOnlyList<OrderLine> Lineas,
    IReadOnlyList<RecipeItem> Receta,
    IReadOnlyList<IngredientStock> Ingredientes);

// ---- Lo que decide el planificador ----
public sealed record IngredientDeduction(
    int IngredienteId, string Nombre, string UnidadMedida,
    decimal Cantidad, decimal StockAnterior, decimal StockResultante);

public sealed record StockAlertDraft(
    int IngredienteId, string Nombre, string UnidadMedida, decimal StockAlMomento, decimal StockMinimo);

public sealed record DeductionPlan(
    IReadOnlyList<IngredientDeduction> Descuentos,
    IReadOnlyList<StockAlertDraft> Alertas);

public sealed record StockShortage(int IngredienteId, string Nombre, decimal Requerido, decimal Disponible);

// ---- Lo que devuelve la API ----
public sealed record OrderConfirmationResult(
    int PedidoId,
    string Estado,
    IReadOnlyList<IngredientDeduction> Descuentos,
    IReadOnlyList<StockAlertDraft> Alertas);

public sealed record StockAlertDto(
    int AlertaId, int IngredienteId, string Ingrediente, string UnidadMedida,
    decimal StockAlMomento, decimal StockMinimo, string Estado,
    DateTime Fecha, DateTime? FechaAtencion);
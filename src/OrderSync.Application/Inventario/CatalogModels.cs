namespace OrderSync.Application.Inventario;

public static class MovementTypes
{
    public const string Venta = "VENTA";
    public const string Recepcion = "RECEPCION";
    public const string Merma = "MERMA";
    public const string Ajuste = "AJUSTE";
    public const string Cancelacion = "CANCELACION";

    public static readonly IReadOnlyList<string> All = [Venta, Recepcion, Merma, Ajuste, Cancelacion];
}

// ---- Ingredientes ----
public sealed record IngredientDto(
    int IngredienteId, string Nombre, string UnidadMedida, decimal StockActual, decimal StockMinimo, bool Activo);

public sealed record CreateIngredientRequest(string Nombre, string UnidadMedida, decimal StockMinimo, decimal StockInicial);

public sealed record UpdateIngredientRequest(string Nombre, string UnidadMedida, decimal StockMinimo);

// ---- Recetas ----
public sealed record RecipeLineDto(int IngredienteId, string Ingrediente, string UnidadMedida, decimal Cantidad);

public sealed record RecipeLineInput(int IngredienteId, decimal Cantidad);

public sealed record RecipeRequest(IReadOnlyList<RecipeLineInput> Lineas);

// ---- Mermas y ajustes ----
public sealed record WasteRequest(int IngredienteId, decimal Cantidad, string Motivo);

public sealed record AdjustmentRequest(int IngredienteId, decimal StockReal, string Motivo);

/// <summary>Cambio de stock ya decidido: el almacén lo aplica junto con su movimiento y su alerta.</summary>
public sealed record StockChange(
    int IngredienteId, string Tipo, decimal Delta, decimal NuevoStock, decimal StockMinimo,
    bool AlertaAbierta, int? UsuarioId, string Motivo);

public sealed record StockChangeResult(
    int IngredienteId, string Ingrediente, string Tipo, decimal Delta,
    decimal StockAnterior, decimal StockActual, decimal StockMinimo, bool BajoMinimo);

// ---- Movimientos ----
public sealed record MovementDto(
    long MovimientoId, int IngredienteId, string Ingrediente, string Tipo, decimal Cantidad,
    int? PedidoId, int? UsuarioId, string? Motivo, DateTime Fecha);

public sealed record MovementFilter(
    int? IngredienteId, string? Tipo, DateTime? Desde, DateTime? Hasta, int Pagina = 1, int TamanoPagina = 50);

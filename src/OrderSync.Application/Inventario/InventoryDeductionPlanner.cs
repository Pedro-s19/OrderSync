namespace OrderSync.Application.Inventario;

/// <summary>
/// SRP: solo calcula. No toca base de datos. Descuenta los ingredientes de la receta
/// (FR-06) y genera alerta cuando el stock queda por debajo del mínimo (FR-07).
/// </summary>
public sealed class InventoryDeductionPlanner : IInventoryDeductionPlanner
{
    public DeductionPlan Plan(OrderInventorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Lineas.Count == 0)
            throw new EmptyOrderException(snapshot.PedidoId);

        // Total requerido por ingrediente (varios productos pueden usar el mismo).
        var recetaPorProducto = snapshot.Receta.ToLookup(r => r.ProductoId);
        var requerido = new Dictionary<int, decimal>();
        foreach (var linea in snapshot.Lineas)
        {
            foreach (var item in recetaPorProducto[linea.ProductoId])
            {
                requerido.TryGetValue(item.IngredienteId, out var acumulado);
                requerido[item.IngredienteId] = acumulado + item.Cantidad * linea.Cantidad;
            }
        }

        var stocks = snapshot.Ingredientes.ToDictionary(i => i.IngredienteId);
        var faltantes = new List<StockShortage>();
        var descuentos = new List<IngredientDeduction>();
        var alertas = new List<StockAlertDraft>();

        foreach (var (ingredienteId, cantidad) in requerido.OrderBy(k => k.Key))
        {
            if (!stocks.TryGetValue(ingredienteId, out var stock))
                throw new InvalidOperationException(
                    $"El ingrediente {ingredienteId} de la receta no existe en el inventario.");

            if (stock.StockActual < cantidad)
            {
                faltantes.Add(new StockShortage(ingredienteId, stock.Nombre, cantidad, stock.StockActual));
                continue;
            }

            var resultante = stock.StockActual - cantidad;
            descuentos.Add(new IngredientDeduction(
                ingredienteId, stock.Nombre, stock.UnidadMedida, cantidad, stock.StockActual, resultante));

            // Alerta solo si queda estrictamente por debajo del mínimo.
            if (resultante < stock.StockMinimo)
                alertas.Add(new StockAlertDraft(
                    ingredienteId, stock.Nombre, stock.UnidadMedida, resultante, stock.StockMinimo));
        }

        if (faltantes.Count > 0)
            throw new InsufficientStockException(snapshot.PedidoId, faltantes);

        return new DeductionPlan(descuentos, alertas);
    }
}
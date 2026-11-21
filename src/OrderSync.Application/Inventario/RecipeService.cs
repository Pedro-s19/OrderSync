using OrderSync.Application.Common;
using OrderSync.Application.Pedidos;

namespace OrderSync.Application.Inventario;

/// <summary>Receta de un producto: qué ingredientes se descuentan al venderlo (FR-06).</summary>
public sealed class RecipeService
{
    private readonly IProductStore _products;
    private readonly IIngredientStore _ingredients;
    private readonly IRecipeStore _recipes;

    public RecipeService(IProductStore products, IIngredientStore ingredients, IRecipeStore recipes)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _ingredients = ingredients ?? throw new ArgumentNullException(nameof(ingredients));
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
    }

    public async Task<IReadOnlyList<RecipeLineDto>> GetAsync(int productoId, CancellationToken ct = default)
    {
        await EnsureProductAsync(productoId, ct);
        return await _recipes.GetAsync(productoId, ct);
    }

    /// <summary>Reemplaza la receta completa. Una lista vacía deja el producto sin descuento de inventario.</summary>
    public async Task<IReadOnlyList<RecipeLineDto>> ReplaceAsync(
        int productoId, RecipeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureProductAsync(productoId, ct);

        IReadOnlyList<RecipeLineInput> lineas = request.Lineas ?? [];
        if (lineas.Any(l => l.Cantidad <= 0))
            throw new RequestValidationException("La cantidad de cada ingrediente debe ser mayor que cero.");

        var ids = lineas.Select(l => l.IngredienteId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new RequestValidationException("Un ingrediente no puede repetirse en la receta.");

        var existentes = (await _ingredients.GetManyAsync(ids, ct)).ToDictionary(i => i.IngredienteId);
        foreach (var id in ids)
        {
            if (!existentes.TryGetValue(id, out var ingrediente))
                throw new ResourceNotFoundException($"No existe el ingrediente {id}.");
            if (!ingrediente.Activo)
                throw new BusinessRuleException($"El ingrediente {ingrediente.Nombre} está desactivado.");
        }

        await _recipes.ReplaceAsync(productoId, lineas, ct);
        return await _recipes.GetAsync(productoId, ct);
    }

    private async Task EnsureProductAsync(int productoId, CancellationToken ct)
    {
        if (await _products.GetAsync(productoId, ct) is null)
            throw new ResourceNotFoundException($"No existe el producto {productoId}.");
    }
}

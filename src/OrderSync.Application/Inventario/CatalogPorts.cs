namespace OrderSync.Application.Inventario;

public interface IIngredientStore
{
    Task<IReadOnlyList<IngredientDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default);
    Task<IngredientDto?> GetAsync(int ingredienteId, CancellationToken ct = default);
    Task<IReadOnlyList<IngredientDto>> GetManyAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string nombre, int? excludeId, CancellationToken ct = default);

    /// <summary>Crea el ingrediente; si trae stock inicial, deja un movimiento AJUSTE que lo explica.</summary>
    Task<IngredientDto> CreateAsync(CreateIngredientRequest ingredient, CancellationToken ct = default);

    Task UpdateAsync(int ingredienteId, UpdateIngredientRequest ingredient, CancellationToken ct = default);
    Task SetActiveAsync(int ingredienteId, bool activo, CancellationToken ct = default);

    /// <summary>Bloquea el ingrediente (dentro de una transacción) y devuelve su stock actual.</summary>
    Task<IngredientStock?> LockAsync(int ingredienteId, CancellationToken ct = default);
}

public interface IRecipeStore
{
    Task<IReadOnlyList<RecipeLineDto>> GetAsync(int productoId, CancellationToken ct = default);
    Task ReplaceAsync(int productoId, IReadOnlyList<RecipeLineInput> lineas, CancellationToken ct = default);
}

/// <summary>Aplica un cambio de stock: actualiza el ingrediente, registra el movimiento y abre o cierra la alerta.</summary>
public interface IStockMovementStore
{
    Task ApplyAsync(StockChange change, CancellationToken ct = default);
}

public interface IStockAlertStore
{
    Task<string?> GetStateAsync(int alertaId, CancellationToken ct = default);
    Task MarkAttendedAsync(int alertaId, CancellationToken ct = default);
}

public interface IMovementReader
{
    Task<IReadOnlyList<MovementDto>> ListAsync(MovementFilter filter, CancellationToken ct = default);
}

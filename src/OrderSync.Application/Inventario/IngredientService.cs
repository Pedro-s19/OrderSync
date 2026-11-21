using OrderSync.Application.Common;

namespace OrderSync.Application.Inventario;

/// <summary>
/// CRUD de ingredientes. El stock NO se edita aquí: cambia solo con ventas, mermas, ajustes
/// o recepciones, para que cada variación quede registrada en MovimientoInventario.
/// </summary>
public sealed class IngredientService
{
    private readonly IIngredientStore _store;

    public IngredientService(IIngredientStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<IngredientDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default)
        => _store.ListAsync(incluirInactivos, ct);

    public async Task<IngredientDto> GetAsync(int ingredienteId, CancellationToken ct = default)
        => await _store.GetAsync(ingredienteId, ct)
           ?? throw new ResourceNotFoundException($"No existe el ingrediente {ingredienteId}.");

    public async Task<IngredientDto> CreateAsync(CreateIngredientRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (nombre, unidad) = ValidateText(request.Nombre, request.UnidadMedida);
        if (request.StockMinimo < 0)
            throw new RequestValidationException("El stock mínimo no puede ser negativo.");
        if (request.StockInicial < 0)
            throw new RequestValidationException("El stock inicial no puede ser negativo.");

        if (await _store.NameExistsAsync(nombre, null, ct))
            throw new ConflictException($"Ya existe un ingrediente llamado {nombre}.");

        return await _store.CreateAsync(request with { Nombre = nombre, UnidadMedida = unidad }, ct);
    }

    public async Task<IngredientDto> UpdateAsync(int ingredienteId, UpdateIngredientRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (nombre, unidad) = ValidateText(request.Nombre, request.UnidadMedida);
        if (request.StockMinimo < 0)
            throw new RequestValidationException("El stock mínimo no puede ser negativo.");

        _ = await GetAsync(ingredienteId, ct);
        if (await _store.NameExistsAsync(nombre, ingredienteId, ct))
            throw new ConflictException($"Ya existe un ingrediente llamado {nombre}.");

        await _store.UpdateAsync(ingredienteId, request with { Nombre = nombre, UnidadMedida = unidad }, ct);
        return await GetAsync(ingredienteId, ct);
    }

    public async Task<IngredientDto> SetActiveAsync(int ingredienteId, bool activo, CancellationToken ct = default)
    {
        _ = await GetAsync(ingredienteId, ct);
        await _store.SetActiveAsync(ingredienteId, activo, ct);
        return await GetAsync(ingredienteId, ct);
    }

    private static (string Nombre, string Unidad) ValidateText(string? nombre, string? unidad)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > 100)
            throw new RequestValidationException("El nombre es obligatorio y admite hasta 100 caracteres.");
        if (string.IsNullOrWhiteSpace(unidad) || unidad.Trim().Length > 10)
            throw new RequestValidationException("La unidad de medida es obligatoria y admite hasta 10 caracteres.");
        return (nombre.Trim(), unidad.Trim());
    }
}

using OrderSync.Application.Common;
using OrderSync.Application.Inventario;

namespace OrderSync.Application.Proveedores;

/// <summary>Directorio de proveedores (FR-19), desactivación (FR-23) y lista de precios para compararlos (FR-20).</summary>
public sealed class SupplierService
{
    private const decimal MaxPrice = 100_000_000m;

    private readonly ISupplierStore _store;
    private readonly IIngredientStore _ingredients;
    private readonly IClock _clock;

    public SupplierService(ISupplierStore store, IIngredientStore ingredients, IClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _ingredients = ingredients ?? throw new ArgumentNullException(nameof(ingredients));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<IReadOnlyList<SupplierDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default)
        => _store.ListAsync(incluirInactivos, ct);

    public async Task<SupplierDto> GetAsync(int proveedorId, CancellationToken ct = default)
        => await _store.GetAsync(proveedorId, ct)
           ?? throw new ResourceNotFoundException($"No existe el proveedor {proveedorId}.");

    public async Task<SupplierDto> CreateAsync(SupplierRequest request, CancellationToken ct = default)
    {
        var clean = Validate(request);
        if (await _store.NameExistsAsync(clean.Nombre, null, ct))
            throw new ConflictException($"Ya existe un proveedor llamado {clean.Nombre}.");
        return await _store.CreateAsync(clean, ct);
    }

    public async Task<SupplierDto> UpdateAsync(int proveedorId, SupplierRequest request, CancellationToken ct = default)
    {
        var clean = Validate(request);
        _ = await GetAsync(proveedorId, ct);
        if (await _store.NameExistsAsync(clean.Nombre, proveedorId, ct))
            throw new ConflictException($"Ya existe un proveedor llamado {clean.Nombre}.");

        await _store.UpdateAsync(proveedorId, clean, ct);
        return await GetAsync(proveedorId, ct);
    }

    /// <summary>Un proveedor desactivado no aparece en comparaciones ni admite órdenes de compra nuevas.</summary>
    public async Task<SupplierDto> SetActiveAsync(int proveedorId, bool activo, CancellationToken ct = default)
    {
        _ = await GetAsync(proveedorId, ct);
        await _store.SetActiveAsync(proveedorId, activo, ct);
        return await GetAsync(proveedorId, ct);
    }

    public async Task<IReadOnlyList<SupplierPriceDto>> GetPricesAsync(int proveedorId, CancellationToken ct = default)
    {
        _ = await GetAsync(proveedorId, ct);
        return await _store.GetPricesAsync(proveedorId, ct);
    }

    public async Task<IReadOnlyList<SupplierPriceDto>> SetPriceAsync(
        int proveedorId, int ingredienteId, PriceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PrecioUnitario <= 0 || request.PrecioUnitario > MaxPrice)
            throw new RequestValidationException("El precio debe ser mayor que cero y hasta 100.000.000.");

        var supplier = await GetAsync(proveedorId, ct);
        if (!supplier.Activo)
            throw new BusinessRuleException($"El proveedor {supplier.Nombre} está desactivado.");

        var ingredient = await _ingredients.GetAsync(ingredienteId, ct)
                         ?? throw new ResourceNotFoundException($"No existe el ingrediente {ingredienteId}.");
        if (!ingredient.Activo)
            throw new BusinessRuleException($"El ingrediente {ingredient.Nombre} está desactivado.");

        await _store.UpsertPriceAsync(
            proveedorId, ingredienteId, request.PrecioUnitario, DateOnly.FromDateTime(_clock.LocalNow), ct);
        return await _store.GetPricesAsync(proveedorId, ct);
    }

    public async Task RemovePriceAsync(int proveedorId, int ingredienteId, CancellationToken ct = default)
    {
        _ = await GetAsync(proveedorId, ct);
        if (!await _store.RemovePriceAsync(proveedorId, ingredienteId, ct))
            throw new ResourceNotFoundException(
                $"El proveedor {proveedorId} no tiene precio registrado para el ingrediente {ingredienteId}.");
    }

    public async Task<IReadOnlyList<PriceComparisonDto>> ComparePricesAsync(int ingredienteId, CancellationToken ct = default)
    {
        if (await _ingredients.GetAsync(ingredienteId, ct) is null)
            throw new ResourceNotFoundException($"No existe el ingrediente {ingredienteId}.");
        return await _store.ComparePricesAsync(ingredienteId, ct);
    }

    private static SupplierRequest Validate(SupplierRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SupplierRequest(
            InputRules.Required(request.Nombre, 120, "El nombre"),
            InputRules.Required(request.Contacto, 100, "El contacto"),
            InputRules.Optional(request.Telefono, 20, "El teléfono"),
            InputRules.OptionalEmail(request.Email));
    }
}

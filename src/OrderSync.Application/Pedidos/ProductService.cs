using OrderSync.Application.Common;

namespace OrderSync.Application.Pedidos;

/// <summary>CRUD del menú. "Desactivar" no borra: el producto deja de poder venderse.</summary>
public sealed class ProductService
{
    private readonly IProductStore _store;

    public ProductService(IProductStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<ProductInfo>> ListAsync(bool incluirInactivos, CancellationToken ct = default)
        => _store.ListAsync(incluirInactivos, ct);

    public async Task<ProductInfo> GetAsync(int productoId, CancellationToken ct = default)
        => await _store.GetAsync(productoId, ct)
           ?? throw new ResourceNotFoundException($"No existe el producto {productoId}.");

    public async Task<ProductInfo> CreateAsync(ProductRequest request, CancellationToken ct = default)
    {
        var clean = Validate(request);
        if (await _store.NameExistsAsync(clean.Nombre, null, ct))
            throw new ConflictException($"Ya existe un producto llamado {clean.Nombre}.");
        return await _store.CreateAsync(clean, ct);
    }

    public async Task<ProductInfo> UpdateAsync(int productoId, ProductRequest request, CancellationToken ct = default)
    {
        var clean = Validate(request);
        _ = await GetAsync(productoId, ct);
        if (await _store.NameExistsAsync(clean.Nombre, productoId, ct))
            throw new ConflictException($"Ya existe un producto llamado {clean.Nombre}.");

        await _store.UpdateAsync(productoId, clean, ct);
        return await GetAsync(productoId, ct);
    }

    public async Task<ProductInfo> SetActiveAsync(int productoId, bool activo, CancellationToken ct = default)
    {
        _ = await GetAsync(productoId, ct);
        await _store.SetActiveAsync(productoId, activo, ct);
        return await GetAsync(productoId, ct);
    }

    private static ProductRequest Validate(ProductRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Nombre) || request.Nombre.Trim().Length > 100)
            throw new RequestValidationException("El nombre es obligatorio y admite hasta 100 caracteres.");

        var categoria = string.IsNullOrWhiteSpace(request.Categoria) ? null : request.Categoria.Trim();
        if (categoria is { Length: > 50 })
            throw new RequestValidationException("La categoría admite hasta 50 caracteres.");

        if (request.Precio < 0 || request.Precio > 100_000_000m)
            throw new RequestValidationException("El precio debe estar entre 0 y 100.000.000.");

        return new ProductRequest(request.Nombre.Trim(), categoria, request.Precio);
    }
}

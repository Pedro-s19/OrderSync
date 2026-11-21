using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Pedidos;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class ProductStore : IProductStore
{
    private readonly OrderSyncDbContext _db;

    public ProductStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<ProductInfo>> ListAsync(bool incluirInactivos, CancellationToken ct = default)
        => await _db.Productos.AsNoTracking()
            .Where(p => incluirInactivos || p.Activo)
            .OrderBy(p => p.Categoria).ThenBy(p => p.Nombre)
            .Select(p => new ProductInfo(p.ProductoId, p.Nombre, p.Categoria, p.Precio, p.Activo))
            .ToListAsync(ct);

    public Task<ProductInfo?> GetAsync(int productoId, CancellationToken ct = default)
        => _db.Productos.AsNoTracking()
            .Where(p => p.ProductoId == productoId)
            .Select(p => new ProductInfo(p.ProductoId, p.Nombre, p.Categoria, p.Precio, p.Activo))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ProductInfo>> GetManyAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        var lista = ids.ToList();
        return await _db.Productos.AsNoTracking()
            .Where(p => lista.Contains(p.ProductoId))
            .Select(p => new ProductInfo(p.ProductoId, p.Nombre, p.Categoria, p.Precio, p.Activo))
            .ToListAsync(ct);
    }

    public Task<bool> NameExistsAsync(string nombre, int? excludeId, CancellationToken ct = default)
        => _db.Productos.AnyAsync(p => p.Nombre == nombre && (excludeId == null || p.ProductoId != excludeId), ct);

    public async Task<ProductInfo> CreateAsync(ProductRequest product, CancellationToken ct = default)
    {
        var entity = new Producto
        {
            Nombre = product.Nombre, Categoria = product.Categoria, Precio = product.Precio, Activo = true
        };
        _db.Productos.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new ProductInfo(entity.ProductoId, entity.Nombre, entity.Categoria, entity.Precio, entity.Activo);
    }

    public async Task UpdateAsync(int productoId, ProductRequest product, CancellationToken ct = default)
    {
        var entity = await _db.Productos.FirstAsync(p => p.ProductoId == productoId, ct);
        entity.Nombre = product.Nombre;
        entity.Categoria = product.Categoria;
        entity.Precio = product.Precio;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int productoId, bool activo, CancellationToken ct = default)
    {
        var entity = await _db.Productos.FirstAsync(p => p.ProductoId == productoId, ct);
        entity.Activo = activo;
        await _db.SaveChangesAsync(ct);
    }
}

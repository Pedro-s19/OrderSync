using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Proveedores;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class SupplierStore : ISupplierStore
{
    private readonly OrderSyncDbContext _db;

    public SupplierStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default)
        => await _db.Proveedors.AsNoTracking()
            .Where(p => incluirInactivos || p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new SupplierDto(p.ProveedorId, p.Nombre, p.Contacto, p.Telefono, p.Email, p.Activo))
            .ToListAsync(ct);

    public Task<SupplierDto?> GetAsync(int proveedorId, CancellationToken ct = default)
        => _db.Proveedors.AsNoTracking()
            .Where(p => p.ProveedorId == proveedorId)
            .Select(p => new SupplierDto(p.ProveedorId, p.Nombre, p.Contacto, p.Telefono, p.Email, p.Activo))
            .FirstOrDefaultAsync(ct);

    public Task<bool> NameExistsAsync(string nombre, int? excludeId, CancellationToken ct = default)
        => _db.Proveedors.AnyAsync(p => p.Nombre == nombre && (excludeId == null || p.ProveedorId != excludeId), ct);

    public async Task<SupplierDto> CreateAsync(SupplierRequest supplier, CancellationToken ct = default)
    {
        var entity = new Proveedor
        {
            Nombre = supplier.Nombre, Contacto = supplier.Contacto,
            Telefono = supplier.Telefono, Email = supplier.Email, Activo = true
        };
        _db.Proveedors.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new SupplierDto(entity.ProveedorId, entity.Nombre, entity.Contacto, entity.Telefono, entity.Email, entity.Activo);
    }

    public async Task UpdateAsync(int proveedorId, SupplierRequest supplier, CancellationToken ct = default)
    {
        var entity = await _db.Proveedors.FirstAsync(p => p.ProveedorId == proveedorId, ct);
        entity.Nombre = supplier.Nombre;
        entity.Contacto = supplier.Contacto;
        entity.Telefono = supplier.Telefono;
        entity.Email = supplier.Email;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int proveedorId, bool activo, CancellationToken ct = default)
    {
        var entity = await _db.Proveedors.FirstAsync(p => p.ProveedorId == proveedorId, ct);
        entity.Activo = activo;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SupplierPriceDto>> GetPricesAsync(int proveedorId, CancellationToken ct = default)
        => await _db.ProveedorIngredientes.AsNoTracking()
            .Where(p => p.ProveedorId == proveedorId)
            .OrderBy(p => p.Ingrediente.Nombre)
            .Select(p => new SupplierPriceDto(
                p.IngredienteId, p.Ingrediente.Nombre, p.Ingrediente.UnidadMedida, p.PrecioUnitario, p.FechaPrecio))
            .ToListAsync(ct);

    public async Task UpsertPriceAsync(int proveedorId, int ingredienteId, decimal precio, DateOnly fecha, CancellationToken ct = default)
    {
        var entity = await _db.ProveedorIngredientes
            .FirstOrDefaultAsync(p => p.ProveedorId == proveedorId && p.IngredienteId == ingredienteId, ct);

        if (entity is null)
            _db.ProveedorIngredientes.Add(new ProveedorIngrediente
            {
                ProveedorId = proveedorId, IngredienteId = ingredienteId, PrecioUnitario = precio, FechaPrecio = fecha
            });
        else
        {
            entity.PrecioUnitario = precio;
            entity.FechaPrecio = fecha;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemovePriceAsync(int proveedorId, int ingredienteId, CancellationToken ct = default)
    {
        var entity = await _db.ProveedorIngredientes
            .FirstOrDefaultAsync(p => p.ProveedorId == proveedorId && p.IngredienteId == ingredienteId, ct);
        if (entity is null) return false;

        _db.ProveedorIngredientes.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PriceComparisonDto>> ComparePricesAsync(int ingredienteId, CancellationToken ct = default)
        => await _db.ProveedorIngredientes.AsNoTracking()
            .Where(p => p.IngredienteId == ingredienteId && p.Proveedor.Activo)
            .OrderBy(p => p.PrecioUnitario).ThenBy(p => p.Proveedor.Nombre)
            .Select(p => new PriceComparisonDto(p.ProveedorId, p.Proveedor.Nombre, p.PrecioUnitario, p.FechaPrecio))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, decimal>> GetPriceMapAsync(
        int proveedorId, IReadOnlyCollection<int> ingredienteIds, CancellationToken ct = default)
    {
        var ids = ingredienteIds.ToList();
        return await _db.ProveedorIngredientes.AsNoTracking()
            .Where(p => p.ProveedorId == proveedorId && ids.Contains(p.IngredienteId))
            .ToDictionaryAsync(p => p.IngredienteId, p => p.PrecioUnitario, ct);
    }
}

namespace OrderSync.Application.Proveedores;

public sealed record SupplierDto(
    int ProveedorId, string Nombre, string Contacto, string? Telefono, string? Email, bool Activo);

public sealed record SupplierRequest(string Nombre, string Contacto, string? Telefono, string? Email);

public sealed record SupplierPriceDto(
    int IngredienteId, string Ingrediente, string UnidadMedida, decimal PrecioUnitario, DateOnly FechaPrecio);

public sealed record PriceRequest(decimal PrecioUnitario);

public sealed record PriceComparisonDto(int ProveedorId, string Proveedor, decimal PrecioUnitario, DateOnly FechaPrecio);

public interface ISupplierStore
{
    Task<IReadOnlyList<SupplierDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default);
    Task<SupplierDto?> GetAsync(int proveedorId, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string nombre, int? excludeId, CancellationToken ct = default);
    Task<SupplierDto> CreateAsync(SupplierRequest supplier, CancellationToken ct = default);
    Task UpdateAsync(int proveedorId, SupplierRequest supplier, CancellationToken ct = default);
    Task SetActiveAsync(int proveedorId, bool activo, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierPriceDto>> GetPricesAsync(int proveedorId, CancellationToken ct = default);
    Task UpsertPriceAsync(int proveedorId, int ingredienteId, decimal precio, DateOnly fecha, CancellationToken ct = default);
    Task<bool> RemovePriceAsync(int proveedorId, int ingredienteId, CancellationToken ct = default);

    /// <summary>Precios de un ingrediente entre los proveedores ACTIVOS, del más barato al más caro (FR-20).</summary>
    Task<IReadOnlyList<PriceComparisonDto>> ComparePricesAsync(int ingredienteId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, decimal>> GetPriceMapAsync(
        int proveedorId, IReadOnlyCollection<int> ingredienteIds, CancellationToken ct = default);
}

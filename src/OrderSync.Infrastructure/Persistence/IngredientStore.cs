using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Inventario;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class IngredientStore : IIngredientStore
{
    private readonly OrderSyncDbContext _db;

    public IngredientStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<IngredientDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default)
        => await _db.Ingredientes.AsNoTracking()
            .Where(i => incluirInactivos || i.Activo)
            .OrderBy(i => i.Nombre)
            .Select(i => new IngredientDto(i.IngredienteId, i.Nombre, i.UnidadMedida, i.StockActual, i.StockMinimo, i.Activo))
            .ToListAsync(ct);

    public Task<IngredientDto?> GetAsync(int ingredienteId, CancellationToken ct = default)
        => _db.Ingredientes.AsNoTracking()
            .Where(i => i.IngredienteId == ingredienteId)
            .Select(i => new IngredientDto(i.IngredienteId, i.Nombre, i.UnidadMedida, i.StockActual, i.StockMinimo, i.Activo))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<IngredientDto>> GetManyAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        var lista = ids.ToList();
        return await _db.Ingredientes.AsNoTracking()
            .Where(i => lista.Contains(i.IngredienteId))
            .Select(i => new IngredientDto(i.IngredienteId, i.Nombre, i.UnidadMedida, i.StockActual, i.StockMinimo, i.Activo))
            .ToListAsync(ct);
    }

    public Task<bool> NameExistsAsync(string nombre, int? excludeId, CancellationToken ct = default)
        => _db.Ingredientes.AnyAsync(i => i.Nombre == nombre && (excludeId == null || i.IngredienteId != excludeId), ct);

    public async Task<IngredientDto> CreateAsync(CreateIngredientRequest ingredient, CancellationToken ct = default)
    {
        var entity = new Ingrediente
        {
            Nombre = ingredient.Nombre,
            UnidadMedida = ingredient.UnidadMedida,
            StockActual = ingredient.StockInicial,
            StockMinimo = ingredient.StockMinimo,
            Activo = true
        };
        _db.Ingredientes.Add(entity);

        // El stock inicial queda explicado por un movimiento (trazabilidad).
        if (ingredient.StockInicial > 0)
            _db.MovimientoInventarios.Add(new MovimientoInventario
            {
                Ingrediente = entity,
                Tipo = MovementTypes.Ajuste,
                Cantidad = ingredient.StockInicial,
                Motivo = "Stock inicial",
                Fecha = DateTime.UtcNow
            });

        await _db.SaveChangesAsync(ct);
        return new IngredientDto(entity.IngredienteId, entity.Nombre, entity.UnidadMedida,
            entity.StockActual, entity.StockMinimo, entity.Activo);
    }

    public async Task UpdateAsync(int ingredienteId, UpdateIngredientRequest ingredient, CancellationToken ct = default)
    {
        var entity = await _db.Ingredientes.FirstAsync(i => i.IngredienteId == ingredienteId, ct);
        entity.Nombre = ingredient.Nombre;
        entity.UnidadMedida = ingredient.UnidadMedida;
        entity.StockMinimo = ingredient.StockMinimo;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int ingredienteId, bool activo, CancellationToken ct = default)
    {
        var entity = await _db.Ingredientes.FirstAsync(i => i.IngredienteId == ingredienteId, ct);
        entity.Activo = activo;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IngredientStock?> LockAsync(int ingredienteId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlAsync(
            $"SELECT IngredienteId FROM inventario.Ingrediente WITH (UPDLOCK, ROWLOCK) WHERE IngredienteId = {ingredienteId}", ct);

        return await _db.Ingredientes.AsNoTracking()
            .Where(i => i.IngredienteId == ingredienteId)
            .Select(i => new IngredientStock(i.IngredienteId, i.Nombre, i.UnidadMedida, i.StockActual, i.StockMinimo))
            .FirstOrDefaultAsync(ct);
    }
}

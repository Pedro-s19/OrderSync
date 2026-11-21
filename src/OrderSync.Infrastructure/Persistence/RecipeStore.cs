using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Inventario;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class RecipeStore : IRecipeStore
{
    private readonly OrderSyncDbContext _db;

    public RecipeStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<RecipeLineDto>> GetAsync(int productoId, CancellationToken ct = default)
        => await _db.Receta.AsNoTracking()
            .Where(r => r.ProductoId == productoId)
            .OrderBy(r => r.IngredienteId)
            .Select(r => new RecipeLineDto(r.IngredienteId, r.Ingrediente.Nombre, r.Ingrediente.UnidadMedida, r.Cantidad))
            .ToListAsync(ct);

    public async Task ReplaceAsync(int productoId, IReadOnlyList<RecipeLineInput> lineas, CancellationToken ct = default)
    {
        var actuales = await _db.Receta.Where(r => r.ProductoId == productoId).ToListAsync(ct);
        var nuevos = lineas.ToDictionary(l => l.IngredienteId);

        // Se actualiza en sitio lo que ya existe (la llave es compuesta), se agrega lo nuevo y se quita lo demás.
        foreach (var existente in actuales)
        {
            if (nuevos.TryGetValue(existente.IngredienteId, out var linea))
                existente.Cantidad = linea.Cantidad;
            else
                _db.Receta.Remove(existente);
        }

        var yaExisten = actuales.Select(a => a.IngredienteId).ToHashSet();
        foreach (var linea in lineas.Where(l => !yaExisten.Contains(l.IngredienteId)))
            _db.Receta.Add(new Recetum
            {
                ProductoId = productoId, IngredienteId = linea.IngredienteId, Cantidad = linea.Cantidad
            });

        await _db.SaveChangesAsync(ct);
    }
}

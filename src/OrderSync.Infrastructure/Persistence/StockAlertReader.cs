using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Inventario;

namespace OrderSync.Infrastructure.Persistence;

public sealed class StockAlertReader : IStockAlertReader
{
    private readonly OrderSyncDbContext _db;

    public StockAlertReader(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<StockAlertDto>> GetAsync(string? estado, CancellationToken ct = default)
    {
        var query = _db.AlertaStocks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(a => a.Estado == estado);

        return await query
            .OrderByDescending(a => a.Fecha)
            .Select(a => new StockAlertDto(
                a.AlertaId,
                a.IngredienteId,
                a.Ingrediente.Nombre,
                a.Ingrediente.UnidadMedida,
                a.StockAlMomento,
                a.StockMinimo,
                a.Estado,
                a.Fecha,
                a.FechaAtencion))
            .ToListAsync(ct);
    }
}
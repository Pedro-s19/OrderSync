using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Crm;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class PromotionStore : IPromotionStore
{
    private readonly OrderSyncDbContext _db;

    public PromotionStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<PromotionDto>> ListAsync(bool incluirInactivas, CancellationToken ct = default)
        => await _db.Promocions.AsNoTracking()
            .Where(p => incluirInactivas || p.Activa)
            .OrderBy(p => p.Nombre)
            .Select(p => new PromotionDto(p.PromocionId, p.Nombre, p.PorcentajeDto, p.PuntosMinimos,
                p.VigenteDesde, p.VigenteHasta, p.Activa))
            .ToListAsync(ct);

    public Task<PromotionDto?> GetAsync(int promocionId, CancellationToken ct = default)
        => _db.Promocions.AsNoTracking()
            .Where(p => p.PromocionId == promocionId)
            .Select(p => new PromotionDto(p.PromocionId, p.Nombre, p.PorcentajeDto, p.PuntosMinimos,
                p.VigenteDesde, p.VigenteHasta, p.Activa))
            .FirstOrDefaultAsync(ct);

    public async Task<PromotionDto> CreateAsync(PromotionRequest promotion, CancellationToken ct = default)
    {
        var entity = new Promocion
        {
            Nombre = promotion.Nombre, PorcentajeDto = promotion.PorcentajeDto, PuntosMinimos = promotion.PuntosMinimos,
            VigenteDesde = promotion.VigenteDesde, VigenteHasta = promotion.VigenteHasta, Activa = true
        };
        _db.Promocions.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new PromotionDto(entity.PromocionId, entity.Nombre, entity.PorcentajeDto, entity.PuntosMinimos,
            entity.VigenteDesde, entity.VigenteHasta, entity.Activa);
    }

    public async Task UpdateAsync(int promocionId, PromotionRequest promotion, CancellationToken ct = default)
    {
        var entity = await _db.Promocions.FirstAsync(p => p.PromocionId == promocionId, ct);
        entity.Nombre = promotion.Nombre;
        entity.PorcentajeDto = promotion.PorcentajeDto;
        entity.PuntosMinimos = promotion.PuntosMinimos;
        entity.VigenteDesde = promotion.VigenteDesde;
        entity.VigenteHasta = promotion.VigenteHasta;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int promocionId, bool activa, CancellationToken ct = default)
    {
        var entity = await _db.Promocions.FirstAsync(p => p.PromocionId == promocionId, ct);
        entity.Activa = activa;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PromotionDto>> GetAssignedAsync(int clienteId, CancellationToken ct = default)
        => await _db.Clientes.AsNoTracking()
            .Where(c => c.ClienteId == clienteId)
            .SelectMany(c => c.Promocions)
            .OrderBy(p => p.Nombre)
            .Select(p => new PromotionDto(p.PromocionId, p.Nombre, p.PorcentajeDto, p.PuntosMinimos,
                p.VigenteDesde, p.VigenteHasta, p.Activa))
            .ToListAsync(ct);

    public Task<bool> IsAssignedAsync(int clienteId, int promocionId, CancellationToken ct = default)
        => _db.Clientes.AnyAsync(c => c.ClienteId == clienteId && c.Promocions.Any(p => p.PromocionId == promocionId), ct);

    public async Task AssignAsync(int clienteId, int promocionId, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.Include(c => c.Promocions).FirstAsync(c => c.ClienteId == clienteId, ct);
        var promocion = await _db.Promocions.FirstAsync(p => p.PromocionId == promocionId, ct);
        cliente.Promocions.Add(promocion);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> UnassignAsync(int clienteId, int promocionId, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.Include(c => c.Promocions).FirstAsync(c => c.ClienteId == clienteId, ct);
        var promocion = cliente.Promocions.FirstOrDefault(p => p.PromocionId == promocionId);
        if (promocion is null) return false;

        cliente.Promocions.Remove(promocion);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

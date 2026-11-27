using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Rrhh;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class ShiftStore : IShiftStore
{
    private readonly OrderSyncDbContext _db;

    public ShiftStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<ShiftDto>> ListAsync(ShiftFilter filter, CancellationToken ct = default)
    {
        var query = _db.Turnos.AsNoTracking().AsQueryable();
        if (filter.EmpleadoId is not null) query = query.Where(t => t.EmpleadoId == filter.EmpleadoId);
        if (filter.Estado is not null) query = query.Where(t => t.Estado == filter.Estado);
        if (filter.Desde is not null)
        {
            var desde = filter.Desde.Value.Date;
            query = query.Where(t => t.Inicio >= desde);
        }
        if (filter.Hasta is not null)
        {
            var hastaExclusivo = filter.Hasta.Value.Date.AddDays(1);
            query = query.Where(t => t.Inicio < hastaExclusivo);
        }

        return await query
            .OrderByDescending(t => t.Inicio)
            .Skip((filter.Pagina - 1) * filter.TamanoPagina)
            .Take(filter.TamanoPagina)
            .Select(t => new ShiftDto(t.TurnoId, t.EmpleadoId, t.Empleado.Nombre, t.Inicio, t.Fin, t.Estado))
            .ToListAsync(ct);
    }

    public Task<ShiftDto?> GetAsync(int turnoId, CancellationToken ct = default)
        => _db.Turnos.AsNoTracking()
            .Where(t => t.TurnoId == turnoId)
            .Select(t => new ShiftDto(t.TurnoId, t.EmpleadoId, t.Empleado.Nombre, t.Inicio, t.Fin, t.Estado))
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasOverlapAsync(int empleadoId, DateTime inicio, DateTime fin, int? excludeTurnoId, CancellationToken ct = default)
        => _db.Turnos.AnyAsync(t =>
            t.EmpleadoId == empleadoId
            && t.Estado != ShiftStatus.Cancelado
            && t.Inicio < fin
            && t.Fin > inicio
            && (excludeTurnoId == null || t.TurnoId != excludeTurnoId), ct);

    public async Task<ShiftDto> CreateAsync(ShiftRequest shift, CancellationToken ct = default)
    {
        var entity = new Turno
        {
            EmpleadoId = shift.EmpleadoId, Inicio = shift.Inicio, Fin = shift.Fin, Estado = ShiftStatus.Programado
        };
        _db.Turnos.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetAsync(entity.TurnoId, ct))!;
    }

    public async Task UpdateTimesAsync(int turnoId, DateTime inicio, DateTime fin, CancellationToken ct = default)
    {
        var entity = await _db.Turnos.FirstAsync(t => t.TurnoId == turnoId, ct);
        entity.Inicio = inicio;
        entity.Fin = fin;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetStateAsync(int turnoId, string estado, CancellationToken ct = default)
    {
        var entity = await _db.Turnos.FirstAsync(t => t.TurnoId == turnoId, ct);
        entity.Estado = estado;
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> HasAttendanceAsync(int turnoId, CancellationToken ct = default)
        => _db.Asistencia.AnyAsync(a => a.TurnoId == turnoId, ct);

    public Task<ShiftDto?> FindCoveringAsync(int empleadoId, DateTime ahora, TimeSpan anticipo, CancellationToken ct = default)
    {
        var limite = ahora.Add(anticipo);
        return _db.Turnos.AsNoTracking()
            .Where(t => t.EmpleadoId == empleadoId
                        && t.Estado == ShiftStatus.Programado
                        && t.Inicio <= limite
                        && t.Fin >= ahora)
            .OrderBy(t => t.Inicio)
            .Select(t => new ShiftDto(t.TurnoId, t.EmpleadoId, t.Empleado.Nombre, t.Inicio, t.Fin, t.Estado))
            .FirstOrDefaultAsync(ct);
    }
}

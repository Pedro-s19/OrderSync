using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Rrhh;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class AttendanceStore : IAttendanceStore
{
    private readonly OrderSyncDbContext _db;

    public AttendanceStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<AttendanceDto>> ListAsync(AttendanceFilter filter, CancellationToken ct = default)
    {
        var query = _db.Asistencia.AsNoTracking().AsQueryable();
        if (filter.EmpleadoId is not null) query = query.Where(a => a.Turno.EmpleadoId == filter.EmpleadoId);
        if (filter.SoloAbiertas) query = query.Where(a => a.Salida == null);
        if (filter.Desde is not null)
        {
            var desde = filter.Desde.Value.Date;
            query = query.Where(a => a.Entrada >= desde);
        }
        if (filter.Hasta is not null)
        {
            var hastaExclusivo = filter.Hasta.Value.Date.AddDays(1);
            query = query.Where(a => a.Entrada < hastaExclusivo);
        }

        var filas = await Project(query.OrderByDescending(a => a.Entrada)
                .Skip((filter.Pagina - 1) * filter.TamanoPagina)
                .Take(filter.TamanoPagina))
            .ToListAsync(ct);

        return filas.Select(ToDto).ToList();
    }

    public async Task<AttendanceDto?> GetAsync(int asistenciaId, CancellationToken ct = default)
    {
        var fila = await Project(_db.Asistencia.AsNoTracking().Where(a => a.AsistenciaId == asistenciaId))
            .FirstOrDefaultAsync(ct);
        return fila is null ? null : ToDto(fila);
    }

    public async Task<AttendanceDto?> FindOpenAsync(int empleadoId, CancellationToken ct = default)
    {
        var fila = await Project(_db.Asistencia.AsNoTracking()
                .Where(a => a.Turno.EmpleadoId == empleadoId && a.Salida == null)
                .OrderByDescending(a => a.Entrada))
            .FirstOrDefaultAsync(ct);
        return fila is null ? null : ToDto(fila);
    }

    public async Task<AttendanceDto> OpenAsync(int turnoId, DateTime entrada, CancellationToken ct = default)
    {
        var entity = new Asistencium { TurnoId = turnoId, Entrada = entrada };
        _db.Asistencia.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetAsync(entity.AsistenciaId, ct))!;
    }

    public async Task CloseAsync(int asistenciaId, DateTime salida, CancellationToken ct = default)
    {
        var entity = await _db.Asistencia.Include(a => a.Turno).FirstAsync(a => a.AsistenciaId == asistenciaId, ct);
        entity.Salida = salida;
        entity.Turno.Estado = ShiftStatus.Cumplido;
        await _db.SaveChangesAsync(ct);
    }

    public async Task CorrectAsync(int asistenciaId, DateTime entrada, DateTime? salida, CancellationToken ct = default)
    {
        var entity = await _db.Asistencia.Include(a => a.Turno).FirstAsync(a => a.AsistenciaId == asistenciaId, ct);
        entity.Entrada = entrada;
        entity.Salida = salida;
        if (salida is not null)
            entity.Turno.Estado = ShiftStatus.Cumplido;
        await _db.SaveChangesAsync(ct);
    }

    private static IQueryable<AttendanceRow> Project(IQueryable<Asistencium> query)
        => query.Select(a => new AttendanceRow
        {
            AsistenciaId = a.AsistenciaId,
            TurnoId = a.TurnoId,
            EmpleadoId = a.Turno.EmpleadoId,
            Empleado = a.Turno.Empleado.Nombre,
            Entrada = a.Entrada,
            Salida = a.Salida,
            TurnoInicio = a.Turno.Inicio,
            TurnoFin = a.Turno.Fin
        });

    private static AttendanceDto ToDto(AttendanceRow r)
        => new(r.AsistenciaId, r.TurnoId, r.EmpleadoId, r.Empleado, r.Entrada, r.Salida, r.TurnoInicio, r.TurnoFin,
            r.Salida is DateTime s ? Math.Round((decimal)(s - r.Entrada).TotalHours, 2) : null);

    private sealed class AttendanceRow
    {
        public int AsistenciaId { get; set; }
        public int TurnoId { get; set; }
        public int EmpleadoId { get; set; }
        public string Empleado { get; set; } = "";
        public DateTime Entrada { get; set; }
        public DateTime? Salida { get; set; }
        public DateTime TurnoInicio { get; set; }
        public DateTime TurnoFin { get; set; }
    }
}

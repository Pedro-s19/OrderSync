using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Nomina;
using OrderSync.Domain.Nomina;

namespace OrderSync.Infrastructure.Persistence;

/// <summary>Lee Empleado, Turno y Asistencia y los convierte en WorkedShift.</summary>
public sealed class AttendanceReader : IAttendanceReader
{
    private const string TurnoCancelado = "CANCELADO";

    private readonly OrderSyncDbContext _db;

    public AttendanceReader(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<EmployeePayrollData>> GetAsync(
        int anio, int mes, int? empleadoId, CancellationToken ct = default)
    {
        var desde = new DateTime(anio, mes, 1);
        var hasta = desde.AddMonths(1);

        var empleadosQuery = _db.Empleados.AsNoTracking();
        if (empleadoId.HasValue)
            empleadosQuery = empleadosQuery.Where(e => e.EmpleadoId == empleadoId.Value);

        var empleados = await empleadosQuery
            .OrderBy(e => e.EmpleadoId)
            .Select(e => new { e.EmpleadoId, e.Nombre, e.TarifaHora, e.Activo })
            .ToListAsync(ct);

        var ids = empleados.Select(e => e.EmpleadoId).ToList();

        var asistencias = await _db.Asistencia
            .AsNoTracking()
            .Where(a => a.Entrada >= desde && a.Entrada < hasta && ids.Contains(a.Turno.EmpleadoId))
            .Select(a => new { a.Turno.EmpleadoId, a.Entrada, a.Salida, a.Turno.Estado })
            .ToListAsync(ct);

        var porEmpleado = asistencias.ToLookup(a => a.EmpleadoId);

        return empleados
            .Select(e => new EmployeePayrollData(
                e.EmpleadoId,
                e.Nombre,
                e.TarifaHora,
                e.Activo,
                porEmpleado[e.EmpleadoId]
                    .Select(a => new WorkedShift(
                        a.Entrada,
                        a.Salida,
                        TieneTurnoProgramado: a.Estado != TurnoCancelado))
                    .ToList()))
            .ToList();
    }
}
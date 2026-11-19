namespace OrderSync.Application.Nomina;

/// <summary>Puerto de lectura: arma los turnos trabajados desde Asistencia + Turno (FR-14).</summary>
public interface IAttendanceReader
{
    Task<IReadOnlyList<EmployeePayrollData>> GetAsync(
        int anio, int mes, int? empleadoId, CancellationToken ct = default);
}
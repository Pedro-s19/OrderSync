namespace OrderSync.Application.Rrhh;

public static class ShiftStatus
{
    public const string Programado = "PROGRAMADO";
    public const string Cumplido = "CUMPLIDO";
    public const string Cancelado = "CANCELADO";

    public static readonly IReadOnlyList<string> All = [Programado, Cumplido, Cancelado];
}

// ---- Empleados ----
public sealed record EmployeeDto(
    int EmpleadoId, string Documento, string Nombre, string AreaCargo, string TipoContrato,
    decimal TarifaHora, decimal? SalarioMensual, DateOnly FechaIngreso, bool Activo);

public sealed record EmployeeRequest(
    string Documento, string Nombre, string AreaCargo, string? TipoContrato,
    decimal TarifaHora, decimal? SalarioMensual, DateOnly? FechaIngreso);

public sealed record EmployeeData(
    string Documento, string Nombre, string AreaCargo, string TipoContrato,
    decimal TarifaHora, decimal? SalarioMensual, DateOnly FechaIngreso);

public sealed record LinkEmployeeRequest(int? EmpleadoId);

// ---- Turnos ----
public sealed record ShiftDto(int TurnoId, int EmpleadoId, string Empleado, DateTime Inicio, DateTime Fin, string Estado);

public sealed record ShiftRequest(int EmpleadoId, DateTime Inicio, DateTime Fin);

public sealed record ShiftTimesRequest(DateTime Inicio, DateTime Fin);

public sealed record ShiftFilter(
    int? EmpleadoId, string? Estado, DateTime? Desde, DateTime? Hasta, int Pagina = 1, int TamanoPagina = 50);

// ---- Asistencia ----
public sealed record AttendanceDto(
    int AsistenciaId, int TurnoId, int EmpleadoId, string Empleado,
    DateTime Entrada, DateTime? Salida, DateTime TurnoInicio, DateTime TurnoFin, decimal? Horas);

public sealed record AttendanceFilter(
    int? EmpleadoId, DateTime? Desde, DateTime? Hasta, bool SoloAbiertas = false, int Pagina = 1, int TamanoPagina = 50);

public sealed record ClockRequest(int EmpleadoId);

public sealed record CorrectAttendanceRequest(DateTime Entrada, DateTime? Salida);

// ---- Puertos ----
public interface IEmployeeStore
{
    Task<IReadOnlyList<EmployeeDto>> ListAsync(bool incluirInactivos, CancellationToken ct = default);
    Task<EmployeeDto?> GetAsync(int empleadoId, CancellationToken ct = default);
    Task<bool> DocumentExistsAsync(string documento, int? excludeId, CancellationToken ct = default);
    Task<EmployeeDto> CreateAsync(EmployeeData employee, CancellationToken ct = default);
    Task UpdateAsync(int empleadoId, EmployeeData employee, CancellationToken ct = default);
    Task SetActiveAsync(int empleadoId, bool activo, CancellationToken ct = default);
}

public interface IShiftStore
{
    Task<IReadOnlyList<ShiftDto>> ListAsync(ShiftFilter filter, CancellationToken ct = default);
    Task<ShiftDto?> GetAsync(int turnoId, CancellationToken ct = default);

    /// <summary>Hay otro turno no cancelado del empleado que se cruza con el rango (FR-13).</summary>
    Task<bool> HasOverlapAsync(int empleadoId, DateTime inicio, DateTime fin, int? excludeTurnoId, CancellationToken ct = default);

    Task<ShiftDto> CreateAsync(ShiftRequest shift, CancellationToken ct = default);
    Task UpdateTimesAsync(int turnoId, DateTime inicio, DateTime fin, CancellationToken ct = default);
    Task SetStateAsync(int turnoId, string estado, CancellationToken ct = default);
    Task<bool> HasAttendanceAsync(int turnoId, CancellationToken ct = default);

    /// <summary>Turno PROGRAMADO que está en curso (o empieza dentro del margen de anticipo).</summary>
    Task<ShiftDto?> FindCoveringAsync(int empleadoId, DateTime ahora, TimeSpan anticipo, CancellationToken ct = default);
}

public interface IAttendanceStore
{
    Task<IReadOnlyList<AttendanceDto>> ListAsync(AttendanceFilter filter, CancellationToken ct = default);
    Task<AttendanceDto?> GetAsync(int asistenciaId, CancellationToken ct = default);
    Task<AttendanceDto?> FindOpenAsync(int empleadoId, CancellationToken ct = default);
    Task<AttendanceDto> OpenAsync(int turnoId, DateTime entrada, CancellationToken ct = default);

    /// <summary>Registra la salida y marca el turno como CUMPLIDO.</summary>
    Task CloseAsync(int asistenciaId, DateTime salida, CancellationToken ct = default);

    Task CorrectAsync(int asistenciaId, DateTime entrada, DateTime? salida, CancellationToken ct = default);
}

/// <summary>Vínculo entre la cuenta de usuario y su ficha de empleado (para "solo su asistencia").</summary>
public interface IEmployeeLinkStore
{
    Task<int?> GetEmployeeIdAsync(int usuarioId, CancellationToken ct = default);
    Task<bool> UserExistsAsync(int usuarioId, CancellationToken ct = default);
    Task LinkAsync(int usuarioId, int? empleadoId, CancellationToken ct = default);
}

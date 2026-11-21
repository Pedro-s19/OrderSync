using OrderSync.Application.Common;

namespace OrderSync.Application.Rrhh;

/// <summary>
/// Entrada y salida del personal (FR-14). La asistencia SIEMPRE pertenece a un turno programado:
/// sin turno en curso no se puede marcar entrada (CT-04). Las horas se toman de la hora de Colombia.
/// </summary>
public sealed class AttendanceService
{
    public static readonly TimeSpan EarlyClockIn = TimeSpan.FromMinutes(30);

    private readonly IAttendanceStore _attendance;
    private readonly IShiftStore _shifts;
    private readonly IEmployeeStore _employees;
    private readonly IEmployeeLinkStore _links;
    private readonly IClock _clock;

    public AttendanceService(
        IAttendanceStore attendance, IShiftStore shifts, IEmployeeStore employees,
        IEmployeeLinkStore links, IClock clock)
    {
        _attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
        _shifts = shifts ?? throw new ArgumentNullException(nameof(shifts));
        _employees = employees ?? throw new ArgumentNullException(nameof(employees));
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<IReadOnlyList<AttendanceDto>> ListAsync(AttendanceFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        InputRules.Paging(filter.Pagina, filter.TamanoPagina);
        InputRules.DateRange(filter.Desde, filter.Hasta);
        return _attendance.ListAsync(filter, ct);
    }

    public async Task<IReadOnlyList<AttendanceDto>> ListMineAsync(
        int usuarioId, DateTime? desde, DateTime? hasta, CancellationToken ct = default)
    {
        var empleadoId = await RequireEmployeeAsync(usuarioId, ct);
        return await ListAsync(new AttendanceFilter(empleadoId, desde, hasta), ct);
    }

    public async Task<AttendanceDto> ClockInAsync(int empleadoId, CancellationToken ct = default)
    {
        var employee = await _employees.GetAsync(empleadoId, ct)
                       ?? throw new ResourceNotFoundException($"No existe el empleado {empleadoId}.");
        if (!employee.Activo)
            throw new BusinessRuleException($"El empleado {employee.Nombre} está desactivado.");

        if (await _attendance.FindOpenAsync(empleadoId, ct) is not null)
            throw new ConflictException("Ya hay una entrada abierta; registre primero la salida.");

        var ahora = _clock.LocalNow;
        var turno = await _shifts.FindCoveringAsync(empleadoId, ahora, EarlyClockIn, ct)
                    ?? throw new BusinessRuleException(
                        "No hay un turno programado en este momento: la asistencia debe pertenecer a un turno.");

        return await _attendance.OpenAsync(turno.TurnoId, ahora, ct);
    }

    public async Task<AttendanceDto> ClockOutAsync(int empleadoId, CancellationToken ct = default)
    {
        var abierta = await _attendance.FindOpenAsync(empleadoId, ct)
                      ?? throw new ConflictException("No hay una entrada abierta para registrar la salida.");

        var ahora = _clock.LocalNow;
        if (ahora <= abierta.Entrada)
            throw new BusinessRuleException("La salida debe ser posterior a la entrada.");

        await _attendance.CloseAsync(abierta.AsistenciaId, ahora, ct);
        return await _attendance.GetAsync(abierta.AsistenciaId, ct)
               ?? throw new ResourceNotFoundException($"No existe la asistencia {abierta.AsistenciaId}.");
    }

    public async Task<AttendanceDto> ClockInMineAsync(int usuarioId, CancellationToken ct = default)
        => await ClockInAsync(await RequireEmployeeAsync(usuarioId, ct), ct);

    public async Task<AttendanceDto> ClockOutMineAsync(int usuarioId, CancellationToken ct = default)
        => await ClockOutAsync(await RequireEmployeeAsync(usuarioId, ct), ct);

    /// <summary>El supervisor corrige una asistencia (por ejemplo, cierra un turno sin salida).</summary>
    public async Task<AttendanceDto> CorrectAsync(
        int asistenciaId, CorrectAttendanceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Salida is DateTime salida && salida <= request.Entrada)
            throw new RequestValidationException("La salida debe ser posterior a la entrada.");

        _ = await _attendance.GetAsync(asistenciaId, ct)
            ?? throw new ResourceNotFoundException($"No existe la asistencia {asistenciaId}.");

        await _attendance.CorrectAsync(asistenciaId, request.Entrada, request.Salida, ct);
        return await _attendance.GetAsync(asistenciaId, ct)
               ?? throw new ResourceNotFoundException($"No existe la asistencia {asistenciaId}.");
    }

    private async Task<int> RequireEmployeeAsync(int usuarioId, CancellationToken ct)
        => await _links.GetEmployeeIdAsync(usuarioId, ct)
           ?? throw new BusinessRuleException("Su usuario no está vinculado a un empleado; pida al administrador que lo vincule.");
}

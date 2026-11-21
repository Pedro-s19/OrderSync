using OrderSync.Application.Common;

namespace OrderSync.Application.Rrhh;

/// <summary>Programación de turnos (FR-13): valida que un empleado no tenga horarios cruzados.</summary>
public sealed class ShiftService
{
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(16);

    private readonly IShiftStore _shifts;
    private readonly IEmployeeStore _employees;

    public ShiftService(IShiftStore shifts, IEmployeeStore employees)
    {
        _shifts = shifts ?? throw new ArgumentNullException(nameof(shifts));
        _employees = employees ?? throw new ArgumentNullException(nameof(employees));
    }

    public Task<IReadOnlyList<ShiftDto>> ListAsync(ShiftFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var estado = string.IsNullOrWhiteSpace(filter.Estado) ? null : filter.Estado.Trim().ToUpperInvariant();
        if (estado is not null && !ShiftStatus.All.Contains(estado))
            throw new RequestValidationException($"Estado no válido. Use: {string.Join(", ", ShiftStatus.All)}.");
        InputRules.Paging(filter.Pagina, filter.TamanoPagina);
        InputRules.DateRange(filter.Desde, filter.Hasta);

        return _shifts.ListAsync(filter with { Estado = estado }, ct);
    }

    public async Task<ShiftDto> GetAsync(int turnoId, CancellationToken ct = default)
        => await _shifts.GetAsync(turnoId, ct) ?? throw new ResourceNotFoundException($"No existe el turno {turnoId}.");

    public async Task<ShiftDto> CreateAsync(ShiftRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRange(request.Inicio, request.Fin);

        var employee = await _employees.GetAsync(request.EmpleadoId, ct)
                       ?? throw new ResourceNotFoundException($"No existe el empleado {request.EmpleadoId}.");
        if (!employee.Activo)
            throw new BusinessRuleException($"El empleado {employee.Nombre} está desactivado.");

        if (await _shifts.HasOverlapAsync(request.EmpleadoId, request.Inicio, request.Fin, null, ct))
            throw new ConflictException($"{employee.Nombre} ya tiene un turno que se cruza con ese horario.");

        return await _shifts.CreateAsync(request, ct);
    }

    public async Task<ShiftDto> UpdateAsync(int turnoId, ShiftTimesRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRange(request.Inicio, request.Fin);

        var shift = await GetAsync(turnoId, ct);
        if (shift.Estado != ShiftStatus.Programado)
            throw new ConflictException($"El turno {turnoId} está {shift.Estado}; solo se edita un turno PROGRAMADO.");
        if (await _shifts.HasOverlapAsync(shift.EmpleadoId, request.Inicio, request.Fin, turnoId, ct))
            throw new ConflictException($"{shift.Empleado} ya tiene un turno que se cruza con ese horario.");

        await _shifts.UpdateTimesAsync(turnoId, request.Inicio, request.Fin, ct);
        return await GetAsync(turnoId, ct);
    }

    public async Task<ShiftDto> CancelAsync(int turnoId, CancellationToken ct = default)
    {
        var shift = await GetAsync(turnoId, ct);
        if (shift.Estado != ShiftStatus.Programado)
            throw new ConflictException($"El turno {turnoId} está {shift.Estado}; solo se cancela un turno PROGRAMADO.");
        if (await _shifts.HasAttendanceAsync(turnoId, ct))
            throw new ConflictException($"El turno {turnoId} ya tiene asistencia registrada; no se puede cancelar.");

        await _shifts.SetStateAsync(turnoId, ShiftStatus.Cancelado, ct);
        return await GetAsync(turnoId, ct);
    }

    private static void ValidateRange(DateTime inicio, DateTime fin)
    {
        if (fin <= inicio)
            throw new RequestValidationException("El fin del turno debe ser posterior al inicio.");
        if (fin - inicio > MaxDuration)
            throw new RequestValidationException("Un turno no puede durar más de 16 horas.");
    }
}

using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

/// <summary>
/// Caso de uso "calcular nómina del mes". SRP: coordina lectura, motor y guardado.
/// DIP: solo conoce interfaces; no sabe de EF Core ni de SQL Server.
/// </summary>
public sealed class PayrollService
{
    private readonly IPayrollParametersProvider _parameters;
    private readonly IAttendanceReader _attendance;
    private readonly IPayrollLiquidationStore _store;

    public PayrollService(
        IPayrollParametersProvider parameters,
        IAttendanceReader attendance,
        IPayrollLiquidationStore store)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<PayrollRunResult> CalculateAsync(PayrollRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mes is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(request), "El mes debe estar entre 1 y 12.");

        var engine = PayrollEngine.Create(_parameters.Get(request.Anio));
        var employees = await _attendance.GetAsync(request.Anio, request.Mes, request.EmpleadoId, ct);

        if (request.EmpleadoId.HasValue && employees.Count == 0)
            throw new KeyNotFoundException($"No existe el empleado {request.EmpleadoId.Value}.");

        var outcomes = new List<EmployeePayrollOutcome>();
        foreach (var employee in employees)
        {
            // CP-05: el empleado inactivo no entra en la nómina.
            if (!employee.Activo)
            {
                outcomes.Add(new(employee.EmpleadoId, employee.Nombre, PayrollItemStatus.Excluido,
                    "Empleado inactivo: no entra en la nómina.", null));
                continue;
            }

            try
            {
                var result = engine.CalculateMonthly(employee.Turnos, employee.TarifaHora);

                await _store.SaveAsync(new PayrollLiquidation(
                    employee.EmpleadoId, request.UsuarioId, request.Anio, request.Mes, result), ct);

                outcomes.Add(new(employee.EmpleadoId, employee.Nombre, PayrollItemStatus.Liquidado, null, result));
            }
            catch (PayrollValidationException ex)
            {
                // CT-03 / CT-04: el turno no se paga; este empleado no se guarda y los demás siguen.
                outcomes.Add(new(employee.EmpleadoId, employee.Nombre, PayrollItemStatus.Rechazado, ex.Message, null));
            }
        }

        return new PayrollRunResult(request.Anio, request.Mes, outcomes);
    }
}
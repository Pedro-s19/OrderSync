using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

/// <summary>
/// Orquesta el cálculo de nómina. SRP: solo coordina; las reglas viven en
/// IShiftPayCalculator e IDeductionCalculator (DIP: depende de abstracciones).
/// </summary>
public sealed class PayrollEngine
{
    private readonly IShiftPayCalculator _shiftPay;
    private readonly IDeductionCalculator _deductions;

    public PayrollEngine(IShiftPayCalculator shiftPay, IDeductionCalculator deductions)
    {
        _shiftPay = shiftPay ?? throw new ArgumentNullException(nameof(shiftPay));
        _deductions = deductions ?? throw new ArgumentNullException(nameof(deductions));
    }

    /// <summary>Atajo para armar el motor con las reglas de Colombia.</summary>
    public static PayrollEngine Create(PayrollParameters parameters)
        => new(new ShiftPayCalculator(parameters), new ColombiaDeductionCalculator(parameters));

    public ShiftPayResult CalculateShift(WorkedShift shift, decimal hourlyRate)
        => _shiftPay.Calculate(shift, hourlyRate);

    /// <summary>Liquida el mes de un empleado a partir de sus turnos trabajados.</summary>
    public PayrollResult CalculateMonthly(IEnumerable<WorkedShift> shifts, decimal hourlyRate)
    {
        ArgumentNullException.ThrowIfNull(shifts);

        decimal ordinarias = 0m, extra = 0m, bruto = 0m;
        foreach (var shift in shifts)
        {
            var r = _shiftPay.Calculate(shift, hourlyRate);
            ordinarias += r.HorasOrdinarias;
            extra += r.HorasExtra;
            bruto += r.PagoBruto;
        }

        if (bruto == 0m) return PayrollResult.SinHoras;   // CT-05

        return new PayrollResult(ordinarias, extra, bruto, _deductions.Calculate(bruto));
    }

    /// <summary>Calcula deducciones directamente sobre un devengado mensual (CP-01 a CP-04).</summary>
    public PayrollResult CalculateFromGross(decimal grossMonthly)
        => new(0m, 0m, grossMonthly, _deductions.Calculate(grossMonthly));
}

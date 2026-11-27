using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

public sealed class ShiftPayCalculator : IShiftPayCalculator
{
    private readonly PayrollParameters _p;

    public ShiftPayCalculator(PayrollParameters parameters)
        => _p = parameters ?? throw new ArgumentNullException(nameof(parameters));

    public ShiftPayResult Calculate(WorkedShift shift, decimal hourlyRate)
    {
        ArgumentNullException.ThrowIfNull(shift);

        if (hourlyRate <= 0)
            throw new PayrollValidationException("La tarifa por hora debe ser mayor que cero.");
        if (!shift.TieneTurnoProgramado)
            throw new PayrollValidationException("La asistencia no tiene un turno programado (FR-14).");
        if (shift.Salida is null)
            throw new PayrollValidationException("El turno no tiene registro de salida; no se puede liquidar.");
        if (shift.Salida.Value <= shift.Entrada)
            throw new PayrollValidationException("La salida debe ser posterior a la entrada.");

        var total = Math.Round((decimal)(shift.Salida.Value - shift.Entrada).TotalHours, 2);
        var ordinarias = Math.Min(total, _p.HorasJornada);
        var extra = Math.Max(0m, total - _p.HorasJornada);

        var bruto = ordinarias * hourlyRate
                  + extra * hourlyRate * (1m + _p.RecargoExtraDiurna);

        return new ShiftPayResult(ordinarias, extra, Money.ToPesos(bruto));
    }
}

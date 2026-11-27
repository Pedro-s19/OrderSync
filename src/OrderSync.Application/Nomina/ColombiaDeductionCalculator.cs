using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

public sealed class ColombiaDeductionCalculator : IDeductionCalculator
{
    private readonly PayrollParameters _p;

    public ColombiaDeductionCalculator(PayrollParameters parameters)
        => _p = parameters ?? throw new ArgumentNullException(nameof(parameters));

    public MonthlyDeductions Calculate(decimal grossMonthly)
    {
        if (grossMonthly <= 0)
            throw new PayrollValidationException("El salario debe ser mayor que cero.");

        // Salud y pensión se calculan sobre el devengado, sin el auxilio de transporte.
        var salud = Money.ToPesos(grossMonthly * _p.SaludEmpleado);
        var pension = Money.ToPesos(grossMonthly * _p.PensionEmpleado);

        var solidaridad = grossMonthly > _p.LimiteSolidaridadSmmlv * _p.Smmlv
            ? Money.ToPesos(grossMonthly * _p.SolidaridadPensional)
            : 0m;

        // El auxilio aplica hasta dos salarios mínimos, inclusive (CP-04).
        var auxilio = grossMonthly <= _p.LimiteAuxilioSmmlv * _p.Smmlv
            ? _p.AuxilioTransporte
            : 0m;

        var neto = grossMonthly - salud - pension - solidaridad + auxilio;

        // Exoneración de salud, SENA e ICBF del empleador por salario menor a 10 SMMLV.
        var exonerado = grossMonthly < _p.LimiteExoneracionSmmlv * _p.Smmlv;
        var empleador = new EmployerContributions(
            Salud: exonerado ? 0m : Money.ToPesos(grossMonthly * _p.SaludEmpleador),
            Pension: Money.ToPesos(grossMonthly * _p.PensionEmpleador),
            CajaCompensacion: Money.ToPesos(grossMonthly * _p.CajaCompensacion),
            Sena: exonerado ? 0m : Money.ToPesos(grossMonthly * _p.Sena),
            Icbf: exonerado ? 0m : Money.ToPesos(grossMonthly * _p.Icbf));

        return new MonthlyDeductions(salud, pension, solidaridad, auxilio, neto, empleador);
    }
}

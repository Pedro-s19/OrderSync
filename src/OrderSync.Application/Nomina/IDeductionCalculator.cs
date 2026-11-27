using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

/// <summary>Regla de deducciones y aportes sobre el consolidado mensual.</summary>
public interface IDeductionCalculator
{
    MonthlyDeductions Calculate(decimal grossMonthly);
}

using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

/// <summary>Regla de pago de un turno: horas ordinarias, extras y recargo.</summary>
public interface IShiftPayCalculator
{
    ShiftPayResult Calculate(WorkedShift shift, decimal hourlyRate);
}

namespace OrderSync.Domain.Nomina;

public sealed record ShiftPayResult(decimal HorasOrdinarias, decimal HorasExtra, decimal PagoBruto);

public sealed record EmployerContributions(
    decimal Salud,
    decimal Pension,
    decimal CajaCompensacion,
    decimal Sena,
    decimal Icbf)
{
    public static readonly EmployerContributions Zero = new(0m, 0m, 0m, 0m, 0m);
}

public sealed record MonthlyDeductions(
    decimal Salud,
    decimal Pension,
    decimal Solidaridad,
    decimal AuxilioTransporte,
    decimal Neto,
    EmployerContributions Empleador)
{
    public static readonly MonthlyDeductions Zero = new(0m, 0m, 0m, 0m, 0m, EmployerContributions.Zero);
}

public sealed record PayrollResult(
    decimal HorasOrdinarias,
    decimal HorasExtra,
    decimal PagoBruto,
    MonthlyDeductions Deducciones)
{
    public decimal Neto => Deducciones.Neto;

    /// <summary>Empleado activo sin horas en el periodo: pago cero (CT-05).</summary>
    public static readonly PayrollResult SinHoras = new(0m, 0m, 0m, MonthlyDeductions.Zero);
}

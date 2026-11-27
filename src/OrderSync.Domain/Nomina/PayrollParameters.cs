namespace OrderSync.Domain.Nomina;

/// <summary>
/// Parámetros de nómina. Se leen de la tabla rrhh.ParametroNomina, de modo que
/// cambiar un porcentaje no exige tocar el código (caso CP-08).
/// </summary>
public sealed record PayrollParameters(
    decimal Smmlv,
    decimal AuxilioTransporte,
    decimal SaludEmpleado,
    decimal PensionEmpleado,
    decimal SolidaridadPensional,
    decimal LimiteAuxilioSmmlv,
    decimal LimiteSolidaridadSmmlv,
    decimal RecargoExtraDiurna,
    decimal HorasJornada,
    decimal PensionEmpleador,
    decimal CajaCompensacion,
    decimal SaludEmpleador,
    decimal Sena,
    decimal Icbf,
    decimal LimiteExoneracionSmmlv)
{
    /// <summary>Valores de 2026 tomados del informe del Sprint 2 (confirmar en el spike).</summary>
    public static PayrollParameters Colombia2026() => new(
        Smmlv: 1_750_905m,
        AuxilioTransporte: 249_095m,
        SaludEmpleado: 0.04m,
        PensionEmpleado: 0.04m,
        SolidaridadPensional: 0.01m,
        LimiteAuxilioSmmlv: 2m,
        LimiteSolidaridadSmmlv: 4m,
        RecargoExtraDiurna: 0.25m,
        HorasJornada: 8m,
        PensionEmpleador: 0.12m,
        CajaCompensacion: 0.04m,
        SaludEmpleador: 0.085m,
        Sena: 0.02m,
        Icbf: 0.03m,
        LimiteExoneracionSmmlv: 10m);
}

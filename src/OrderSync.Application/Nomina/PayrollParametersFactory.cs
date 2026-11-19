using OrderSync.Domain.Nomina;

namespace OrderSync.Application.Nomina;

/// <summary>
/// Construye PayrollParameters a partir de pares Clave/Valor (lo que hay en
/// rrhh.ParametroNomina). No conoce la base de datos: solo valida y mapea.
/// </summary>
public static class PayrollParametersFactory
{
    public static readonly IReadOnlyList<string> RequiredKeys =
    [
        "SMMLV",
        "AUXILIO_TRANSPORTE",
        "SALUD_EMPLEADO",
        "PENSION_EMPLEADO",
        "SOLIDARIDAD_PENSIONAL",
        "LIMITE_AUXILIO_SMMLV",
        "LIMITE_SOLIDARIDAD_SMMLV",
        "RECARGO_EXTRA_DIURNA",
        "HORAS_JORNADA",
        "PENSION_EMPLEADOR",
        "CAJA_COMPENSACION",
        "SALUD_EMPLEADOR",
        "SENA",
        "ICBF",
        "LIMITE_EXONERACION_SMMLV"
    ];

    public static PayrollParameters FromValues(IReadOnlyDictionary<string, decimal> values, int year)
    {
        ArgumentNullException.ThrowIfNull(values);

        var lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
            lookup[pair.Key.Trim()] = pair.Value;

        var missing = RequiredKeys.Where(k => !lookup.ContainsKey(k)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Faltan parámetros de nómina para {year}: {string.Join(", ", missing)}.");

        return new PayrollParameters(
            Smmlv: lookup["SMMLV"],
            AuxilioTransporte: lookup["AUXILIO_TRANSPORTE"],
            SaludEmpleado: lookup["SALUD_EMPLEADO"],
            PensionEmpleado: lookup["PENSION_EMPLEADO"],
            SolidaridadPensional: lookup["SOLIDARIDAD_PENSIONAL"],
            LimiteAuxilioSmmlv: lookup["LIMITE_AUXILIO_SMMLV"],
            LimiteSolidaridadSmmlv: lookup["LIMITE_SOLIDARIDAD_SMMLV"],
            RecargoExtraDiurna: lookup["RECARGO_EXTRA_DIURNA"],
            HorasJornada: lookup["HORAS_JORNADA"],
            PensionEmpleador: lookup["PENSION_EMPLEADOR"],
            CajaCompensacion: lookup["CAJA_COMPENSACION"],
            SaludEmpleador: lookup["SALUD_EMPLEADOR"],
            Sena: lookup["SENA"],
            Icbf: lookup["ICBF"],
            LimiteExoneracionSmmlv: lookup["LIMITE_EXONERACION_SMMLV"]);
    }
}
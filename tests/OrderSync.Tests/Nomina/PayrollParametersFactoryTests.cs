using OrderSync.Application.Nomina;
using OrderSync.Domain.Nomina;
using Xunit;

namespace OrderSync.Tests.Nomina;

public class PayrollParametersFactoryTests
{
    private static Dictionary<string, decimal> ValoresCompletos() => new()
    {
        ["SMMLV"] = 1_750_905m,
        ["AUXILIO_TRANSPORTE"] = 249_095m,
        ["SALUD_EMPLEADO"] = 0.04m,
        ["PENSION_EMPLEADO"] = 0.04m,
        ["SOLIDARIDAD_PENSIONAL"] = 0.01m,
        ["LIMITE_AUXILIO_SMMLV"] = 2m,
        ["LIMITE_SOLIDARIDAD_SMMLV"] = 4m,
        ["RECARGO_EXTRA_DIURNA"] = 0.25m,
        ["HORAS_JORNADA"] = 8m,
        ["PENSION_EMPLEADOR"] = 0.12m,
        ["CAJA_COMPENSACION"] = 0.04m,
        ["SALUD_EMPLEADOR"] = 0.085m,
        ["SENA"] = 0.02m,
        ["ICBF"] = 0.03m,
        ["LIMITE_EXONERACION_SMMLV"] = 10m
    };

    [Fact]
    public void FromValues_ConTodasLasClaves_ReproduceColombia2026()
    {
        var result = PayrollParametersFactory.FromValues(ValoresCompletos(), 2026);

        Assert.Equal(PayrollParameters.Colombia2026(), result);
    }

    [Fact]
    public void FromValues_CambiarUnPorcentaje_CambiaElResultadoSinTocarCodigo_CP08()
    {
        var values = ValoresCompletos();
        values["SALUD_EMPLEADO"] = 0.05m;

        var result = PayrollParametersFactory.FromValues(values, 2026);

        Assert.Equal(0.05m, result.SaludEmpleado);
    }

    [Fact]
    public void FromValues_ClavesEnMinusculas_SeReconocen()
    {
        var values = ValoresCompletos()
            .ToDictionary(p => p.Key.ToLowerInvariant(), p => p.Value);

        var result = PayrollParametersFactory.FromValues(values, 2026);

        Assert.Equal(1_750_905m, result.Smmlv);
    }

    [Fact]
    public void FromValues_FaltanClaves_LanzaErrorQueLasNombra()
    {
        var values = ValoresCompletos();
        values.Remove("SENA");
        values.Remove("ICBF");

        var ex = Assert.Throws<InvalidOperationException>(
            () => PayrollParametersFactory.FromValues(values, 2026));

        Assert.Contains("SENA", ex.Message);
        Assert.Contains("ICBF", ex.Message);
        Assert.Contains("2026", ex.Message);
    }

    [Fact]
    public void FromValues_DiccionarioNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => PayrollParametersFactory.FromValues(null!, 2026));
    }
}
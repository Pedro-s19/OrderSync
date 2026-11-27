using Moq;
using OrderSync.Application.Nomina;
using OrderSync.Domain.Nomina;
using Xunit;

namespace OrderSync.Tests.Nomina;

public class PayrollEngineTests
{
    private const decimal Tarifa = 10_000m;
    private static readonly DateTime Dia = new(2026, 11, 2);

    private static PayrollEngine Engine() => PayrollEngine.Create(PayrollParameters.Colombia2026());

    private static WorkedShift Turno(int horaEntrada, int horaSalida)
        => new(Dia.AddHours(horaEntrada), Dia.AddHours(horaSalida));

    // ---------- Pago por turno (Tabla 7) ----------

    [Fact] // CT-01
    public void TurnoOrdinario_PagaOchoHorasOrdinarias()
    {
        var r = Engine().CalculateShift(Turno(8, 16), Tarifa);

        Assert.Equal(8m, r.HorasOrdinarias);
        Assert.Equal(0m, r.HorasExtra);
        Assert.Equal(80_000m, r.PagoBruto);
    }

    [Fact] // CT-02
    public void TurnoConHorasExtraDiurnas_AplicaRecargoDel25()
    {
        var r = Engine().CalculateShift(Turno(8, 18), Tarifa); // 8 ordinarias + 2 extra

        Assert.Equal(8m, r.HorasOrdinarias);
        Assert.Equal(2m, r.HorasExtra);
        Assert.Equal(105_000m, r.PagoBruto);
    }

    [Fact] // CT-03
    public void TurnoSinSalida_LanzaErrorDeValidacion()
    {
        var turno = new WorkedShift(Dia.AddHours(8), null);

        Assert.Throws<PayrollValidationException>(() => Engine().CalculateShift(turno, Tarifa));
    }

    [Fact] // CT-04
    public void AsistenciaSinTurnoProgramado_SeRechaza()
    {
        var turno = new WorkedShift(Dia.AddHours(8), Dia.AddHours(16), TieneTurnoProgramado: false);

        Assert.Throws<PayrollValidationException>(() => Engine().CalculateShift(turno, Tarifa));
    }

    [Fact] // CT-05
    public void EmpleadoSinHorasEnElPeriodo_PagaCero()
    {
        var r = Engine().CalculateMonthly(Array.Empty<WorkedShift>(), Tarifa);

        Assert.Equal(0m, r.PagoBruto);
        Assert.Equal(0m, r.Neto);
    }

    [Fact]
    public void SalidaAnteriorALaEntrada_LanzaErrorDeValidacion()
    {
        var turno = new WorkedShift(Dia.AddHours(16), Dia.AddHours(8));

        Assert.Throws<PayrollValidationException>(() => Engine().CalculateShift(turno, Tarifa));
    }

    [Fact]
    public void LiquidacionMensual_SumaLosTurnosYAplicaDeducciones()
    {
        var turnos = new[] { Turno(8, 16), Turno(8, 18) }; // 80.000 + 105.000

        var r = Engine().CalculateMonthly(turnos, Tarifa);

        Assert.Equal(16m, r.HorasOrdinarias);
        Assert.Equal(2m, r.HorasExtra);
        Assert.Equal(185_000m, r.PagoBruto);
        Assert.Equal(7_400m, r.Deducciones.Salud);
        Assert.Equal(7_400m, r.Deducciones.Pension);
    }

    // ---------- Deducciones mensuales (Tabla 8) ----------
    // Los atributos no admiten decimal, por eso los datos van como long.

    [Theory]
    [InlineData(1_750_905L, 70_036L, 70_036L, 0L, 249_095L, 1_859_928L)]   // CP-01 salario mínimo
    [InlineData(4_000_000L, 160_000L, 160_000L, 0L, 0L, 3_680_000L)]       // CP-02 sobre 2 SMMLV
    [InlineData(8_000_000L, 320_000L, 320_000L, 80_000L, 0L, 7_280_000L)]  // CP-03 sobre 4 SMMLV
    [InlineData(3_501_810L, 140_072L, 140_072L, 0L, 249_095L, 3_470_761L)] // CP-04 borde: 2 SMMLV exactos
    public void Deducciones_CoincidenConElCalculoManual(
        long salario, long salud, long pension, long solidaridad, long auxilio, long neto)
    {
        var d = Engine().CalculateFromGross(salario).Deducciones;

        Assert.Equal(salud, d.Salud);
        Assert.Equal(pension, d.Pension);
        Assert.Equal(solidaridad, d.Solidaridad);
        Assert.Equal(auxilio, d.AuxilioTransporte);
        Assert.Equal(neto, d.Neto);
    }

    [Theory] // CP-06
    [InlineData(0L)]
    [InlineData(-1_000L)]
    public void SalarioCeroONegativo_LanzaErrorDeValidacion(long salario)
    {
        Assert.Throws<PayrollValidationException>(() => Engine().CalculateFromGross(salario));
    }

    [Fact] // CP-07
    public void AportesDelEmpleador_ConSalarioMinimo_ExoneranSaludSenaEIcbf()
    {
        var e = Engine().CalculateFromGross(1_750_905m).Deducciones.Empleador;

        Assert.Equal(210_109m, e.Pension);
        Assert.Equal(70_036m, e.CajaCompensacion);
        Assert.Equal(0m, e.Salud);
        Assert.Equal(0m, e.Sena);
        Assert.Equal(0m, e.Icbf);
    }

    [Fact] // CP-08
    public void CambiarUnParametro_CambiaElResultadoSinTocarElCodigo()
    {
        var modificados = PayrollParameters.Colombia2026() with { SaludEmpleado = 0.05m };

        var d = PayrollEngine.Create(modificados).CalculateFromGross(4_000_000m).Deducciones;

        Assert.Equal(200_000m, d.Salud);
        Assert.Equal(3_640_000m, d.Neto);
    }

    // ---------- DIP: el motor solo depende de interfaces ----------

    [Fact]
    public void Motor_DelegaEnLasReglasInyectadas()
    {
        var turno = Turno(8, 16);
        var pagoTurno = new Mock<IShiftPayCalculator>();
        var deducciones = new Mock<IDeductionCalculator>();
        pagoTurno.Setup(c => c.Calculate(turno, Tarifa)).Returns(new ShiftPayResult(8m, 0m, 80_000m));
        deducciones.Setup(c => c.Calculate(80_000m)).Returns(MonthlyDeductions.Zero);

        var r = new PayrollEngine(pagoTurno.Object, deducciones.Object)
            .CalculateMonthly(new[] { turno }, Tarifa);

        Assert.Equal(80_000m, r.PagoBruto);
        pagoTurno.Verify(c => c.Calculate(turno, Tarifa), Times.Once);
        deducciones.Verify(c => c.Calculate(80_000m), Times.Once);
    }
}

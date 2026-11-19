using Moq;
using OrderSync.Application.Nomina;
using OrderSync.Domain.Nomina;
using Xunit;

namespace OrderSync.Tests.Nomina;

public class PayrollServiceTests
{
    private readonly Mock<IPayrollParametersProvider> _parameters = new();
    private readonly Mock<IAttendanceReader> _attendance = new();
    private readonly Mock<IPayrollLiquidationStore> _store = new();
    private readonly List<PayrollLiquidation> _saved = new();

    public PayrollServiceTests()
    {
        _parameters.Setup(p => p.Get(2026)).Returns(PayrollParameters.Colombia2026());
        _store.Setup(s => s.SaveAsync(It.IsAny<PayrollLiquidation>(), It.IsAny<CancellationToken>()))
              .Callback<PayrollLiquidation, CancellationToken>((l, _) => _saved.Add(l))
              .Returns(Task.CompletedTask);
    }

    private PayrollService CreateService() => new(_parameters.Object, _attendance.Object, _store.Object);

    private void GivenEmployees(int? empleadoId = null, params EmployeePayrollData[] data)
        => _attendance
            .Setup(a => a.GetAsync(2026, 3, empleadoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<EmployeePayrollData>)data);

    private static WorkedShift Turno(int dia, int horas) =>
        new(new DateTime(2026, 3, dia, 8, 0, 0), new DateTime(2026, 3, dia, 8, 0, 0).AddHours(horas));

    private static EmployeePayrollData Empleado(
        int id, bool activo = true, params WorkedShift[] turnos) =>
        new(id, $"Empleado {id}", 10_000m, activo, turnos);

    private static PayrollRequest Marzo(int? empleadoId = null, int? usuarioId = null) =>
        new(2026, 3, empleadoId, usuarioId);

    [Fact]
    public async Task TurnoOrdinario_SeLiquidaYSeGuarda_CT01()
    {
        GivenEmployees(null, Empleado(1, true, Turno(2, 8)));

        var result = await CreateService().CalculateAsync(Marzo());

        var item = Assert.Single(result.Empleados);
        Assert.Equal(PayrollItemStatus.Liquidado, item.Estado);
        var saved = Assert.Single(_saved);
        Assert.Equal(80_000m, saved.Resultado.PagoBruto);
        Assert.Equal(8m, saved.Resultado.HorasOrdinarias);
    }

    [Fact]
    public async Task HorasExtraDiurnas_SeLiquidanConRecargo_CT02()
    {
        GivenEmployees(null, Empleado(1, true, Turno(2, 10)));

        await CreateService().CalculateAsync(Marzo());

        var saved = Assert.Single(_saved);
        Assert.Equal(105_000m, saved.Resultado.PagoBruto);
        Assert.Equal(2m, saved.Resultado.HorasExtra);
    }

    [Fact]
    public async Task TurnoSinSalida_SeRechazaYNoSeGuarda_CT03()
    {
        var sinSalida = new WorkedShift(new DateTime(2026, 3, 2, 8, 0, 0), null);
        GivenEmployees(null, Empleado(1, true, sinSalida));

        var result = await CreateService().CalculateAsync(Marzo());

        var item = Assert.Single(result.Empleados);
        Assert.Equal(PayrollItemStatus.Rechazado, item.Estado);
        Assert.False(string.IsNullOrWhiteSpace(item.Mensaje));
        Assert.Null(item.Resultado);
        _store.Verify(s => s.SaveAsync(It.IsAny<PayrollLiquidation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AsistenciaSinTurnoProgramado_SeRechaza_CT04()
    {
        var sinTurno = new WorkedShift(
            new DateTime(2026, 3, 2, 8, 0, 0), new DateTime(2026, 3, 2, 16, 0, 0), TieneTurnoProgramado: false);
        GivenEmployees(null, Empleado(1, true, sinTurno));

        var result = await CreateService().CalculateAsync(Marzo());

        Assert.Equal(PayrollItemStatus.Rechazado, Assert.Single(result.Empleados).Estado);
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task EmpleadoActivoSinHoras_SeGuardaConPagoCero_CT05()
    {
        GivenEmployees(null, Empleado(1, true));

        var result = await CreateService().CalculateAsync(Marzo());

        Assert.Equal(PayrollItemStatus.Liquidado, Assert.Single(result.Empleados).Estado);
        Assert.Equal(0m, Assert.Single(_saved).Resultado.PagoBruto);
    }

    [Fact]
    public async Task EmpleadoInactivo_SeExcluyeYNoSeGuarda_CP05()
    {
        GivenEmployees(null, Empleado(1, false, Turno(2, 8)));

        var result = await CreateService().CalculateAsync(Marzo());

        Assert.Equal(PayrollItemStatus.Excluido, Assert.Single(result.Empleados).Estado);
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task UnEmpleadoRechazado_NoDetieneLaLiquidacionDeLosDemas()
    {
        var sinSalida = new WorkedShift(new DateTime(2026, 3, 2, 8, 0, 0), null);
        GivenEmployees(null,
            Empleado(1, true, sinSalida),
            Empleado(2, true, Turno(2, 8)));

        var result = await CreateService().CalculateAsync(Marzo());

        Assert.Equal(PayrollItemStatus.Rechazado, result.Empleados[0].Estado);
        Assert.Equal(PayrollItemStatus.Liquidado, result.Empleados[1].Estado);
        Assert.Equal(2, Assert.Single(_saved).EmpleadoId);
    }

    [Fact]
    public async Task ElUsuarioQueCalcula_SeGuardaEnLaLiquidacion()
    {
        GivenEmployees(null, Empleado(1, true, Turno(2, 8)));

        await CreateService().CalculateAsync(Marzo(usuarioId: 7));

        Assert.Equal(7, Assert.Single(_saved).UsuarioId);
        Assert.Equal(2026, _saved[0].Anio);
        Assert.Equal(3, _saved[0].Mes);
    }

    [Fact]
    public async Task LeeLosParametrosDelAnioSolicitado()
    {
        GivenEmployees(null, Empleado(1, true));

        await CreateService().CalculateAsync(Marzo());

        _parameters.Verify(p => p.Get(2026), Times.Once);
    }

    [Fact]
    public async Task EmpleadoInexistente_LanzaKeyNotFound()
    {
        GivenEmployees(99);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateService().CalculateAsync(Marzo(empleadoId: 99)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task MesInvalido_LanzaArgumentOutOfRange(int mes)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateService().CalculateAsync(new PayrollRequest(2026, mes)));
    }

    [Fact]
    public async Task SolicitudNula_LanzaArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateService().CalculateAsync(null!));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PayrollService(null!, _attendance.Object, _store.Object));
        Assert.Throws<ArgumentNullException>(
            () => new PayrollService(_parameters.Object, null!, _store.Object));
        Assert.Throws<ArgumentNullException>(
            () => new PayrollService(_parameters.Object, _attendance.Object, null!));
    }
}
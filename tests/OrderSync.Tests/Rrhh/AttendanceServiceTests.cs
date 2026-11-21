using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Rrhh;
using OrderSync.Tests.Support;
using Xunit;

namespace OrderSync.Tests.Rrhh;

public class AttendanceServiceTests
{
    private static readonly DateTime Ahora = new(2026, 3, 10, 8, 5, 0);

    private readonly Mock<IAttendanceStore> _attendance = new();
    private readonly Mock<IShiftStore> _shifts = new();
    private readonly Mock<IEmployeeStore> _employees = new();
    private readonly Mock<IEmployeeLinkStore> _links = new();
    private readonly FakeClock _clock = new(Ahora);

    private AttendanceService Create() => new(_attendance.Object, _shifts.Object, _employees.Object, _links.Object, _clock);

    private static EmployeeDto Emp(bool activo = true)
        => new(1, "1001", "Ana", "Cocina", "TERMINO_FIJO", 10_000m, null, new DateOnly(2026, 1, 5), activo);

    private static ShiftDto Turno() => new(3, 1, "Ana", new DateTime(2026, 3, 10, 8, 0, 0), new DateTime(2026, 3, 10, 16, 0, 0), "PROGRAMADO");

    private static AttendanceDto Asistencia(DateTime entrada, DateTime? salida = null)
        => new(10, 3, 1, "Ana", entrada, salida, new DateTime(2026, 3, 10, 8, 0, 0), new DateTime(2026, 3, 10, 16, 0, 0), null);

    private void GivenEmployee(EmployeeDto? employee)
        => _employees.Setup(e => e.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

    private void GivenOpen(AttendanceDto? open)
        => _attendance.Setup(a => a.FindOpenAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(open);

    private void GivenCoveringShift(ShiftDto? shift)
        => _shifts.Setup(s => s.FindCoveringAsync(1, Ahora, AttendanceService.EarlyClockIn, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(shift);

    private void GivenLinked(int? empleadoId)
        => _links.Setup(l => l.GetEmployeeIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(empleadoId);

    // ---------- Entrada ----------

    [Fact]
    public async Task Entrada_ConTurnoEnCurso_SeVinculaAlTurno()
    {
        GivenEmployee(Emp());
        GivenOpen(null);
        GivenCoveringShift(Turno());
        _attendance.Setup(a => a.OpenAsync(3, Ahora, It.IsAny<CancellationToken>())).ReturnsAsync(Asistencia(Ahora));

        var result = await Create().ClockInAsync(1);

        Assert.Equal(3, result.TurnoId);
        _attendance.Verify(a => a.OpenAsync(3, Ahora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Entrada_SinTurnoProgramado_LanzaReglaDeNegocio_CT04()
    {
        GivenEmployee(Emp());
        GivenOpen(null);
        GivenCoveringShift(null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().ClockInAsync(1));

        _attendance.Verify(a => a.OpenAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Entrada_ConUnaEntradaYaAbierta_LanzaConflicto()
    {
        GivenEmployee(Emp());
        GivenOpen(Asistencia(Ahora.AddHours(-1)));

        await Assert.ThrowsAsync<ConflictException>(() => Create().ClockInAsync(1));
    }

    [Fact]
    public async Task Entrada_EmpleadoInexistente_LanzaNotFound()
    {
        GivenEmployee(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().ClockInAsync(1));
    }

    [Fact]
    public async Task Entrada_EmpleadoDesactivado_LanzaReglaDeNegocio()
    {
        GivenEmployee(Emp(activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().ClockInAsync(1));
    }

    // ---------- Salida ----------

    [Fact]
    public async Task Salida_ConEntradaAbierta_CierraLaAsistencia()
    {
        var abierta = Asistencia(new DateTime(2026, 3, 10, 8, 0, 0));
        GivenOpen(abierta);
        _attendance.Setup(a => a.GetAsync(10, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Asistencia(abierta.Entrada, Ahora));

        var result = await Create().ClockOutAsync(1);

        Assert.Equal(Ahora, result.Salida);
        _attendance.Verify(a => a.CloseAsync(10, Ahora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Salida_SinEntradaAbierta_LanzaConflicto()
    {
        GivenOpen(null);

        await Assert.ThrowsAsync<ConflictException>(() => Create().ClockOutAsync(1));
    }

    [Fact]
    public async Task Salida_NoPosteriorALaEntrada_LanzaReglaDeNegocio()
    {
        GivenOpen(Asistencia(Ahora));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().ClockOutAsync(1));

        _attendance.Verify(a => a.CloseAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Solo su asistencia ----------

    [Fact]
    public async Task MiEntrada_UsaElEmpleadoVinculadoAlUsuario()
    {
        GivenLinked(1);
        GivenEmployee(Emp());
        GivenOpen(null);
        GivenCoveringShift(Turno());
        _attendance.Setup(a => a.OpenAsync(3, Ahora, It.IsAny<CancellationToken>())).ReturnsAsync(Asistencia(Ahora));

        var result = await Create().ClockInMineAsync(9);

        Assert.Equal(1, result.EmpleadoId);
    }

    [Fact]
    public async Task MiSalida_UsaElEmpleadoVinculadoAlUsuario()
    {
        GivenLinked(1);
        GivenOpen(Asistencia(new DateTime(2026, 3, 10, 8, 0, 0)));
        _attendance.Setup(a => a.GetAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(Asistencia(Ahora, Ahora));

        await Create().ClockOutMineAsync(9);

        _attendance.Verify(a => a.CloseAsync(10, Ahora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MiAsistencia_FiltraPorElEmpleadoVinculado()
    {
        GivenLinked(1);
        _attendance.Setup(a => a.ListAsync(It.IsAny<AttendanceFilter>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((IReadOnlyList<AttendanceDto>)[]);

        await Create().ListMineAsync(9, null, null);

        _attendance.Verify(a => a.ListAsync(It.Is<AttendanceFilter>(f => f.EmpleadoId == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UsuarioSinEmpleadoVinculado_LanzaReglaDeNegocio()
    {
        GivenLinked(null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().ClockInMineAsync(9));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().ClockOutMineAsync(9));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().ListMineAsync(9, null, null));
    }

    // ---------- Corrección y consulta ----------

    [Fact]
    public async Task Corregir_CierraUnTurnoSinSalida()
    {
        var entrada = new DateTime(2026, 3, 10, 8, 0, 0);
        var salida = new DateTime(2026, 3, 10, 16, 0, 0);
        _attendance.Setup(a => a.GetAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(Asistencia(entrada));

        await Create().CorrectAsync(10, new CorrectAttendanceRequest(entrada, salida));

        _attendance.Verify(a => a.CorrectAsync(10, entrada, salida, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Corregir_ConSalidaNoPosteriorALaEntrada_LanzaValidacion()
    {
        var entrada = new DateTime(2026, 3, 10, 8, 0, 0);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CorrectAsync(10, new CorrectAttendanceRequest(entrada, entrada)));
    }

    [Fact]
    public async Task Corregir_AsistenciaInexistente_LanzaNotFound()
    {
        _attendance.Setup(a => a.GetAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync((AttendanceDto?)null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(
            () => Create().CorrectAsync(10, new CorrectAttendanceRequest(Ahora, null)));
    }

    [Fact]
    public async Task Listar_ConFiltrosInvalidos_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ListAsync(new AttendanceFilter(null, null, null, false, 0, 50)));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().ListAsync(new AttendanceFilter(null, Ahora, Ahora.AddDays(-1))));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AttendanceService(null!, _shifts.Object, _employees.Object, _links.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new AttendanceService(_attendance.Object, null!, _employees.Object, _links.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new AttendanceService(_attendance.Object, _shifts.Object, null!, _links.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new AttendanceService(_attendance.Object, _shifts.Object, _employees.Object, null!, _clock));
        Assert.Throws<ArgumentNullException>(() => new AttendanceService(_attendance.Object, _shifts.Object, _employees.Object, _links.Object, null!));
    }
}

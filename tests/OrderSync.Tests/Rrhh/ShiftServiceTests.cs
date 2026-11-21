using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Rrhh;
using Xunit;

namespace OrderSync.Tests.Rrhh;

public class ShiftServiceTests
{
    private static readonly DateTime Inicio = new(2026, 3, 10, 8, 0, 0);
    private static readonly DateTime Fin = new(2026, 3, 10, 16, 0, 0);

    private readonly Mock<IShiftStore> _shifts = new();
    private readonly Mock<IEmployeeStore> _employees = new();

    private ShiftService Create() => new(_shifts.Object, _employees.Object);

    private static EmployeeDto Emp(bool activo = true)
        => new(1, "1001", "Ana", "Cocina", "TERMINO_FIJO", 10_000m, null, new DateOnly(2026, 1, 5), activo);

    private static ShiftDto Shift(string estado = "PROGRAMADO") => new(3, 1, "Ana", Inicio, Fin, estado);

    private void GivenEmployee(EmployeeDto? employee)
        => _employees.Setup(e => e.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

    private void GivenShift(ShiftDto? shift)
        => _shifts.Setup(s => s.GetAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(shift);

    private void GivenOverlap(bool overlap, int? excludeId = null)
        => _shifts.Setup(s => s.HasOverlapAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), excludeId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(overlap);

    // ---------- Crear ----------

    [Fact]
    public async Task Crear_ConHorarioLibre_Programa()
    {
        GivenEmployee(Emp());
        GivenOverlap(false);
        _shifts.Setup(s => s.CreateAsync(It.IsAny<ShiftRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Shift());

        var result = await Create().CreateAsync(new ShiftRequest(1, Inicio, Fin));

        Assert.Equal(3, result.TurnoId);
        _shifts.Verify(s => s.CreateAsync(It.Is<ShiftRequest>(r => r.Inicio == Inicio && r.Fin == Fin), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Crear_ConHorarioCruzado_LanzaConflicto()
    {
        GivenEmployee(Emp());
        GivenOverlap(true);

        await Assert.ThrowsAsync<ConflictException>(() => Create().CreateAsync(new ShiftRequest(1, Inicio, Fin)));

        _shifts.Verify(s => s.CreateAsync(It.IsAny<ShiftRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_EmpleadoInexistente_LanzaNotFound()
    {
        GivenEmployee(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().CreateAsync(new ShiftRequest(1, Inicio, Fin)));
    }

    [Fact]
    public async Task Crear_EmpleadoDesactivado_LanzaReglaDeNegocio()
    {
        GivenEmployee(Emp(activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().CreateAsync(new ShiftRequest(1, Inicio, Fin)));
    }

    [Fact]
    public async Task Crear_ConFinAnteriorOIgualAlInicio_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(new ShiftRequest(1, Fin, Inicio)));
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(new ShiftRequest(1, Inicio, Inicio)));
    }

    [Fact]
    public async Task Crear_ConDuracionMayorADieciseisHoras_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(new ShiftRequest(1, Inicio, Inicio.AddHours(17))));
    }

    [Fact]
    public async Task Crear_ConDuracionDeDieciseisHoras_EsValida()
    {
        GivenEmployee(Emp());
        GivenOverlap(false);
        _shifts.Setup(s => s.CreateAsync(It.IsAny<ShiftRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Shift());

        await Create().CreateAsync(new ShiftRequest(1, Inicio, Inicio.AddHours(16)));

        _shifts.Verify(s => s.CreateAsync(It.IsAny<ShiftRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Crear_ConSolicitudNula_LanzaArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().CreateAsync(null!));
    }

    // ---------- Editar ----------

    [Fact]
    public async Task Editar_TurnoProgramado_ActualizaIgnorandoSuPropioHorario()
    {
        GivenShift(Shift());
        GivenOverlap(false, excludeId: 3);

        await Create().UpdateAsync(3, new ShiftTimesRequest(Inicio.AddHours(1), Fin.AddHours(1)));

        _shifts.Verify(s => s.HasOverlapAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 3, It.IsAny<CancellationToken>()), Times.Once);
        _shifts.Verify(s => s.UpdateTimesAsync(3, Inicio.AddHours(1), Fin.AddHours(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("CUMPLIDO")]
    [InlineData("CANCELADO")]
    public async Task Editar_TurnoQueNoEstaProgramado_LanzaConflicto(string estado)
    {
        GivenShift(Shift(estado));

        await Assert.ThrowsAsync<ConflictException>(() => Create().UpdateAsync(3, new ShiftTimesRequest(Inicio, Fin)));
    }

    [Fact]
    public async Task Editar_ConHorarioCruzado_LanzaConflicto()
    {
        GivenShift(Shift());
        GivenOverlap(true, excludeId: 3);

        await Assert.ThrowsAsync<ConflictException>(() => Create().UpdateAsync(3, new ShiftTimesRequest(Inicio, Fin)));

        _shifts.Verify(s => s.UpdateTimesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Editar_TurnoInexistente_LanzaNotFound()
    {
        GivenShift(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().UpdateAsync(3, new ShiftTimesRequest(Inicio, Fin)));
    }

    // ---------- Cancelar ----------

    [Fact]
    public async Task Cancelar_TurnoProgramadoSinAsistencia_Funciona()
    {
        GivenShift(Shift());
        _shifts.Setup(s => s.HasAttendanceAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Create().CancelAsync(3);

        _shifts.Verify(s => s.SetStateAsync(3, "CANCELADO", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_TurnoConAsistencia_LanzaConflicto()
    {
        GivenShift(Shift());
        _shifts.Setup(s => s.HasAttendanceAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => Create().CancelAsync(3));

        _shifts.Verify(s => s.SetStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("CUMPLIDO")]
    [InlineData("CANCELADO")]
    public async Task Cancelar_TurnoQueNoEstaProgramado_LanzaConflicto(string estado)
    {
        GivenShift(Shift(estado));

        await Assert.ThrowsAsync<ConflictException>(() => Create().CancelAsync(3));
    }

    [Fact]
    public async Task Cancelar_TurnoInexistente_LanzaNotFound()
    {
        GivenShift(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().CancelAsync(3));
    }

    // ---------- Consultar ----------

    [Fact]
    public async Task Listar_NormalizaElEstado()
    {
        _shifts.Setup(s => s.ListAsync(It.IsAny<ShiftFilter>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((IReadOnlyList<ShiftDto>)[]);

        await Create().ListAsync(new ShiftFilter(null, " programado ", null, null));

        _shifts.Verify(s => s.ListAsync(It.Is<ShiftFilter>(f => f.Estado == "PROGRAMADO"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_ConFiltrosInvalidos_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListAsync(new ShiftFilter(null, "VOLANDO", null, null)));
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListAsync(new ShiftFilter(null, null, null, null, 0, 50)));
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().ListAsync(new ShiftFilter(null, null, Fin, Inicio.AddDays(-1))));
    }

    [Fact]
    public async Task Consultar_TurnoInexistente_LanzaNotFound()
    {
        GivenShift(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(3));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ShiftService(null!, _employees.Object));
        Assert.Throws<ArgumentNullException>(() => new ShiftService(_shifts.Object, null!));
    }
}

using Moq;
using OrderSync.Application.Common;
using OrderSync.Application.Rrhh;
using OrderSync.Tests.Support;
using Xunit;

namespace OrderSync.Tests.Rrhh;

public class EmployeeServiceTests
{
    private readonly Mock<IEmployeeStore> _store = new();
    private readonly Mock<IEmployeeLinkStore> _links = new();
    private readonly FakeClock _clock = new(new DateTime(2026, 3, 10, 9, 0, 0));

    private EmployeeService Create() => new(_store.Object, _links.Object, _clock);

    private static EmployeeRequest Request(
        string documento = "1001", string nombre = "Ana", string area = "Cocina",
        string? contrato = null, decimal tarifa = 10_000m, decimal? salario = null, DateOnly? ingreso = null)
        => new(documento, nombre, area, contrato, tarifa, salario, ingreso);

    private static EmployeeDto Emp(bool activo = true)
        => new(1, "1001", "Ana", "Cocina", "TERMINO_FIJO", 10_000m, null, new DateOnly(2026, 1, 5), activo);

    private void GivenEmployee(EmployeeDto? employee)
        => _store.Setup(s => s.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

    [Fact]
    public async Task Crear_AplicaLosValoresPorDefecto()
    {
        EmployeeData? guardado = null;
        _store.Setup(s => s.DocumentExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _store.Setup(s => s.CreateAsync(It.IsAny<EmployeeData>(), It.IsAny<CancellationToken>()))
              .Callback<EmployeeData, CancellationToken>((d, _) => guardado = d)
              .ReturnsAsync(Emp());

        await Create().CreateAsync(Request(documento: " 1001 ", nombre: " Ana "));

        Assert.Equal("1001", guardado!.Documento);
        Assert.Equal("Ana", guardado.Nombre);
        Assert.Equal("TERMINO_FIJO", guardado.TipoContrato);
        Assert.Equal(new DateOnly(2026, 3, 10), guardado.FechaIngreso);
    }

    [Fact]
    public async Task Crear_ConContratoEscrito_LoGuardaEnMayusculas()
    {
        EmployeeData? guardado = null;
        _store.Setup(s => s.DocumentExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _store.Setup(s => s.CreateAsync(It.IsAny<EmployeeData>(), It.IsAny<CancellationToken>()))
              .Callback<EmployeeData, CancellationToken>((d, _) => guardado = d)
              .ReturnsAsync(Emp());

        await Create().CreateAsync(Request(contrato: "termino_indefinido"));

        Assert.Equal("TERMINO_INDEFINIDO", guardado!.TipoContrato);
    }

    [Fact]
    public async Task Crear_ConDocumentoRepetido_LanzaConflicto()
    {
        _store.Setup(s => s.DocumentExistsAsync("1001", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => Create().CreateAsync(Request()));
    }

    [Theory]
    [InlineData("", "Ana", "Cocina", 10000.0)]
    [InlineData("1001", "", "Cocina", 10000.0)]
    [InlineData("1001", "Ana", "", 10000.0)]
    [InlineData("1001", "Ana", "Cocina", 0.0)]
    [InlineData("1001", "Ana", "Cocina", -5.0)]
    public async Task Crear_ConDatosInvalidos_LanzaValidacion(string documento, string nombre, string area, double tarifa)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(Request(documento, nombre, area, tarifa: (decimal)tarifa)));
    }

    [Fact]
    public async Task Crear_ConSalarioNoPositivo_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => Create().CreateAsync(Request(salario: 0m)));
    }

    [Fact]
    public async Task Crear_ConFechaDeIngresoFutura_LanzaValidacion()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => Create().CreateAsync(Request(ingreso: new DateOnly(2026, 3, 11))));
    }

    [Fact]
    public async Task Editar_EmpleadoInexistente_LanzaNotFound()
    {
        GivenEmployee(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().UpdateAsync(1, Request()));
    }

    [Fact]
    public async Task Editar_ConDocumentoDeOtroEmpleado_LanzaConflicto()
    {
        GivenEmployee(Emp());
        _store.Setup(s => s.DocumentExistsAsync("2002", 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => Create().UpdateAsync(1, Request(documento: "2002")));
    }

    [Fact]
    public async Task Editar_Valido_Actualiza()
    {
        GivenEmployee(Emp());
        _store.Setup(s => s.DocumentExistsAsync(It.IsAny<string>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Create().UpdateAsync(1, Request(tarifa: 12_000m));

        _store.Verify(s => s.UpdateAsync(1, It.Is<EmployeeData>(d => d.TarifaHora == 12_000m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivarODesactivar_CambiaElEstado(bool activo)
    {
        GivenEmployee(Emp());

        await Create().SetActiveAsync(1, activo);

        _store.Verify(s => s.SetActiveAsync(1, activo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desactivar_EmpleadoInexistente_LanzaNotFound()
    {
        GivenEmployee(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().SetActiveAsync(1, false));
    }

    [Fact]
    public async Task Vincular_UsuarioInexistente_LanzaNotFound()
    {
        _links.Setup(l => l.UserExistsAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().LinkUserAsync(9, 1));
    }

    [Fact]
    public async Task Vincular_EmpleadoInexistente_LanzaNotFound()
    {
        _links.Setup(l => l.UserExistsAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        GivenEmployee(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().LinkUserAsync(9, 1));
    }

    [Fact]
    public async Task Vincular_EmpleadoDesactivado_LanzaReglaDeNegocio()
    {
        _links.Setup(l => l.UserExistsAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        GivenEmployee(Emp(activo: false));

        await Assert.ThrowsAsync<BusinessRuleException>(() => Create().LinkUserAsync(9, 1));
    }

    [Fact]
    public async Task Vincular_Valido_GuardaElVinculo()
    {
        _links.Setup(l => l.UserExistsAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        GivenEmployee(Emp());

        await Create().LinkUserAsync(9, 1);

        _links.Verify(l => l.LinkAsync(9, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desvincular_ConNulo_NoConsultaElEmpleado()
    {
        _links.Setup(l => l.UserExistsAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Create().LinkUserAsync(9, null);

        _links.Verify(l => l.LinkAsync(9, null, It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consultar_EmpleadoInexistente_LanzaNotFound()
    {
        GivenEmployee(null);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Create().GetAsync(1));
    }

    [Fact]
    public async Task Solicitud_Nula_LanzaArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().CreateAsync(null!));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new EmployeeService(null!, _links.Object, _clock));
        Assert.Throws<ArgumentNullException>(() => new EmployeeService(_store.Object, null!, _clock));
        Assert.Throws<ArgumentNullException>(() => new EmployeeService(_store.Object, _links.Object, null!));
    }
}

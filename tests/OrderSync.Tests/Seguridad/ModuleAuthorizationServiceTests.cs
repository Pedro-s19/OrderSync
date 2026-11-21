using Moq;
using OrderSync.Application.Seguridad;
using Xunit;

namespace OrderSync.Tests.Seguridad;

public class ModuleAuthorizationServiceTests
{
    private readonly Mock<IPermissionReader> _reader = new();

    private ModuleAuthorizationService Create() => new(_reader.Object);

    private void GivenPermission(PermissionInfo? info)
        => _reader.Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(info);

    [Theory]
    [InlineData(2, "Turnos y pago", 2, "pago", true)]             // Supervisor calcula el pago: 200
    [InlineData(2, "Solo su asistencia", 2, "pago", false)]       // Cajero en RR. HH.: 403
    [InlineData(0, null, 2, null, false)]                         // Cajero modifica inventario: 403
    [InlineData(3, null, 3, null, true)]                          // acceso total
    [InlineData(1, null, 1, null, true)]                          // lectura alcanza para consultar
    [InlineData(1, null, 2, null, false)]                         // lectura no alcanza para modificar
    [InlineData(3, null, 3, "pago", true)]                        // total ignora el alcance
    [InlineData(2, null, 2, "pago", false)]                       // parcial sin alcance definido
    [InlineData(2, "TURNOS Y PAGO", 2, "pago", true)]             // alcance sin distinguir mayúsculas
    [InlineData(2, "Cancelar y consultar", 3, null, false)]       // parcial no alcanza nivel total
    public async Task Check_AplicaLaMatrizDePermisos(
        int nivel, string? alcance, int nivelMinimo, string? alcanceRequerido, bool permitido)
    {
        GivenPermission(new PermissionInfo(true, true, nivel, alcance));

        var decision = await Create().CheckAsync(7, "RRHH", nivelMinimo, alcanceRequerido);

        Assert.Equal(permitido, decision.Allowed);
        Assert.Equal(permitido, decision.Reason is null);
    }

    [Fact]
    public async Task UsuarioDesactivado_SeDeniega()
    {
        GivenPermission(new PermissionInfo(false, true, 3, null));

        var decision = await Create().CheckAsync(7, "PEDIDOS", PermissionLevel.Lectura);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task RolDesactivado_SeDeniega()
    {
        GivenPermission(new PermissionInfo(true, false, 3, null));

        var decision = await Create().CheckAsync(7, "PEDIDOS", PermissionLevel.Lectura);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task UsuarioOModuloSinPermiso_SeDeniega()
    {
        GivenPermission(null);

        var decision = await Create().CheckAsync(7, "NO_EXISTE", PermissionLevel.Lectura);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task ConsultaElPermisoDelUsuarioYModuloIndicados()
    {
        GivenPermission(new PermissionInfo(true, true, 3, null));

        await Create().CheckAsync(7, "RRHH", PermissionLevel.Lectura);

        _reader.Verify(r => r.GetAsync(7, "RRHH", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_ConLectorNulo_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ModuleAuthorizationService(null!));
    }
}

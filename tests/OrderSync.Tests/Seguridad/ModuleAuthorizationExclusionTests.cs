using Moq;
using OrderSync.Application.Seguridad;
using Xunit;

namespace OrderSync.Tests.Seguridad;

/// <summary>Regla de exclusión de alcance: deja fuera a quien solo ve "su asistencia" o solo "cobra".</summary>
public class ModuleAuthorizationExclusionTests
{
    private readonly Mock<IPermissionReader> _reader = new();

    private ModuleAuthorizationService Create() => new(_reader.Object);

    private void GivenPermission(int nivel, string? alcance)
        => _reader.Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PermissionInfo(true, true, nivel, alcance));

    [Theory]
    [InlineData(2, "Solo su asistencia", "asistencia", false)]   // Cajero y Cocina en RR. HH.
    [InlineData(2, "Turnos y pago", "asistencia", true)]         // Supervisor
    [InlineData(1, null, "asistencia", true)]                    // Gerencia: lectura sin alcance
    [InlineData(3, "Solo su asistencia", "asistencia", true)]    // el acceso total ignora la exclusión
    [InlineData(2, "Cobro", "cobro", false)]                     // Cajero en Facturación
    [InlineData(2, "Reportes", "cobro", true)]                   // Gerencia en Facturación
    [InlineData(1, "Lectura", "cobro", true)]                    // Supervisor en Facturación
    [InlineData(2, "SOLO SU ASISTENCIA", "asistencia", false)]   // sin distinguir mayúsculas
    public async Task AlcanceNoContiene_ExcluyeSoloAQuienLoTiene(int nivel, string? alcance, string noContiene, bool permitido)
    {
        GivenPermission(nivel, alcance);

        var decision = await Create().CheckAsync(7, "RRHH", PermissionLevel.Lectura, null, CancellationToken.None, noContiene);

        Assert.Equal(permitido, decision.Allowed);
    }

    [Fact]
    public async Task AlcanceContieneYNoContiene_SeCombinan()
    {
        GivenPermission(2, "Turnos y pago");

        var permitido = await Create().CheckAsync(7, "RRHH", PermissionLevel.Parcial, "pago", CancellationToken.None, "asistencia");
        var denegado = await Create().CheckAsync(7, "RRHH", PermissionLevel.Parcial, "pago", CancellationToken.None, "turnos");

        Assert.True(permitido.Allowed);
        Assert.False(denegado.Allowed);
    }

    [Fact]
    public async Task SinParametrosDeAlcance_ElComportamientoAnteriorNoCambia()
    {
        GivenPermission(2, "Solo su asistencia");

        var decision = await Create().CheckAsync(7, "RRHH", PermissionLevel.Lectura);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task ElNivelInsuficiente_SeDeniegaAntesDeMirarElAlcance()
    {
        GivenPermission(1, null);

        var decision = await Create().CheckAsync(7, "RRHH", PermissionLevel.Parcial, null, CancellationToken.None, "asistencia");

        Assert.False(decision.Allowed);
    }
}

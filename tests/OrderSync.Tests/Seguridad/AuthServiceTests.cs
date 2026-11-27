using Moq;
using OrderSync.Application.Seguridad;
using Xunit;

namespace OrderSync.Tests.Seguridad;

public class AuthServiceTests
{
    private readonly Mock<IUserAccountReader> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenGenerator> _tokens = new();
    private static readonly DateTime Expira = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    public AuthServiceTests()
    {
        _tokens.Setup(t => t.Generate(It.IsAny<TokenUser>())).Returns(new IssuedToken("jwt-token", Expira));
    }

    private AuthService Create() => new(_users.Object, _hasher.Object, _tokens.Object);

    private static UserAccount Account(bool activo = true, bool rolActivo = true)
        => new(7, "Ana", "ana@ordersync.local", "HASH", 2, "Cajero", activo, rolActivo);

    private void GivenAccount(UserAccount? account)
        => _users.Setup(u => u.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(account);

    private void GivenPasswordMatches(bool matches)
        => _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(matches);

    private void VerifyNoToken()
        => _tokens.Verify(t => t.Generate(It.IsAny<TokenUser>()), Times.Never);

    [Fact]
    public async Task LoginCorrecto_DevuelveTokenYDatosDelUsuario()
    {
        GivenAccount(Account());
        GivenPasswordMatches(true);

        var result = await Create().LoginAsync(new LoginRequest("ana@ordersync.local", "Clave1234", "Cajero"));

        Assert.Equal("jwt-token", result.Token);
        Assert.Equal(Expira, result.ExpiraUtc);
        Assert.Equal(7, result.UsuarioId);
        Assert.Equal("Cajero", result.Rol);
        _tokens.Verify(t => t.Generate(It.Is<TokenUser>(u =>
            u.UsuarioId == 7 && u.RolId == 2 && u.RolNombre == "Cajero")), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData(null)]
    public async Task ContrasenaConMenosDeOchoCaracteres_LanzaValidacion_YNoConsultaLaBase(string? password)
    {
        await Assert.ThrowsAsync<SecurityValidationException>(
            () => Create().LoginAsync(new LoginRequest("ana@ordersync.local", password!, "Cajero")));

        _users.Verify(u => u.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "Cajero")]
    [InlineData("  ", "Cajero")]
    [InlineData("ana@ordersync.local", "")]
    [InlineData("ana@ordersync.local", "  ")]
    public async Task CorreoORolVacio_LanzaValidacion(string email, string rol)
    {
        await Assert.ThrowsAsync<SecurityValidationException>(
            () => Create().LoginAsync(new LoginRequest(email, "Clave1234", rol)));
    }

    [Fact]
    public async Task UsuarioInexistente_LanzaCredencialesInvalidas()
    {
        GivenAccount(null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => Create().LoginAsync(new LoginRequest("nadie@ordersync.local", "Clave1234", "Cajero")));

        VerifyNoToken();
    }

    [Fact]
    public async Task ContrasenaIncorrecta_LanzaCredencialesInvalidas_SinToken()
    {
        GivenAccount(Account());
        GivenPasswordMatches(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => Create().LoginAsync(new LoginRequest("ana@ordersync.local", "OtraClave99", "Cajero")));

        VerifyNoToken();
    }

    [Fact]
    public async Task RolElegidoDistintoAlDeLaCuenta_LanzaCredencialesInvalidas_SinToken()
    {
        GivenAccount(Account());          // la cuenta es Cajero
        GivenPasswordMatches(true);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => Create().LoginAsync(new LoginRequest("ana@ordersync.local", "Clave1234", "Supervisor")));

        VerifyNoToken();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CuentaORolDesactivado_LanzaCredencialesInvalidas(bool activo, bool rolActivo)
    {
        GivenAccount(Account(activo, rolActivo));
        GivenPasswordMatches(true);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => Create().LoginAsync(new LoginRequest("ana@ordersync.local", "Clave1234", "Cajero")));

        VerifyNoToken();
    }

    [Fact]
    public async Task ElCorreoSeNormaliza_AntesDeBuscar()
    {
        GivenAccount(Account());
        GivenPasswordMatches(true);

        await Create().LoginAsync(new LoginRequest("  ANA@OrderSync.Local ", "Clave1234", "Cajero"));

        _users.Verify(u => u.FindByEmailAsync("ana@ordersync.local", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ElRolSeCompara_SinDistinguirMayusculas()
    {
        GivenAccount(Account());
        GivenPasswordMatches(true);

        var result = await Create().LoginAsync(new LoginRequest("ana@ordersync.local", "Clave1234", "cajero"));

        Assert.Equal("Cajero", result.Rol);
    }

    [Fact]
    public async Task SolicitudNula_LanzaArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().LoginAsync(null!));
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthService(null!, _hasher.Object, _tokens.Object));
        Assert.Throws<ArgumentNullException>(() => new AuthService(_users.Object, null!, _tokens.Object));
        Assert.Throws<ArgumentNullException>(() => new AuthService(_users.Object, _hasher.Object, null!));
    }
}

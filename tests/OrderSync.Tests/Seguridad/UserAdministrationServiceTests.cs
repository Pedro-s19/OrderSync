using Moq;
using OrderSync.Application.Seguridad;
using Xunit;

namespace OrderSync.Tests.Seguridad;

public class UserAdministrationServiceTests
{
    private const int AdminId = 1;

    private readonly Mock<IUserAdminStore> _store = new();
    private readonly Mock<IPasswordHasher> _hasher = new();

    private UserAdministrationService Create() => new(_store.Object, _hasher.Object);

    private static RoleSummary Rol(int id = 2, bool activo = true) => new(id, "Cajero", activo);

    private static UserSummary Usuario(int id = 10, int rolId = 3)
        => new(id, "Ana", "ana@ordersync.local", rolId, "Supervisor", true);

    private static CreateUserRequest Request(string email = "ana@ordersync.local", string password = "Clave1234")
        => new("  Ana  ", email, password, 2);

    private void GivenRole(int rolId, RoleSummary? role)
        => _store.Setup(s => s.GetRoleAsync(rolId, It.IsAny<CancellationToken>())).ReturnsAsync(role);

    private void GivenUser(int usuarioId, UserSummary? user)
        => _store.Setup(s => s.GetUserAsync(usuarioId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    private void VerifyNothingWritten()
    {
        _store.Verify(s => s.CreateUserAsync(
            It.IsAny<int>(), It.IsAny<NewUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.ChangeRoleAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Crear usuario ----------

    [Fact]
    public async Task CrearUsuario_GuardaElHash_NuncaLaContrasena_YNormalizaElCorreo()
    {
        NewUser? guardado = null;
        GivenRole(2, Rol());
        _store.Setup(s => s.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash("Clave1234")).Returns("HASHED");
        _store.Setup(s => s.CreateUserAsync(It.IsAny<int>(), It.IsAny<NewUser>(), It.IsAny<CancellationToken>()))
              .Callback<int, NewUser, CancellationToken>((_, nuevo, _) => guardado = nuevo)
              .ReturnsAsync(new UserSummary(10, "Ana", "ana@ordersync.local", 2, "Cajero", true));

        var result = await Create().CreateUserAsync(AdminId, Request("  ANA@OrderSync.Local "));

        Assert.NotNull(guardado);
        Assert.Equal("HASHED", guardado!.PasswordHash);
        Assert.DoesNotContain("Clave1234", guardado.PasswordHash);
        Assert.Equal("ana@ordersync.local", guardado.Email);
        Assert.Equal("Ana", guardado.Nombre);
        Assert.Equal(10, result.UsuarioId);
        _store.Verify(s => s.CreateUserAsync(AdminId, It.IsAny<NewUser>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CrearUsuario_ConContrasenaCorta_LanzaValidacion_YNoEscribe()
    {
        await Assert.ThrowsAsync<SecurityValidationException>(
            () => Create().CreateUserAsync(AdminId, Request(password: "corta")));

        VerifyNothingWritten();
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("a b@c.com")]
    public async Task CrearUsuario_ConCorreoInvalido_LanzaValidacion(string email)
    {
        await Assert.ThrowsAsync<SecurityValidationException>(
            () => Create().CreateUserAsync(AdminId, Request(email)));

        VerifyNothingWritten();
    }

    [Fact]
    public async Task CrearUsuario_ConRolInexistente_LanzaNotFound()
    {
        GivenRole(2, null);

        await Assert.ThrowsAsync<SecurityNotFoundException>(() => Create().CreateUserAsync(AdminId, Request()));

        VerifyNothingWritten();
    }

    [Fact]
    public async Task CrearUsuario_ConRolDesactivado_LanzaReglaDeNegocio()
    {
        GivenRole(2, Rol(activo: false));

        await Assert.ThrowsAsync<SecurityRuleException>(() => Create().CreateUserAsync(AdminId, Request()));

        VerifyNothingWritten();
    }

    [Fact]
    public async Task CrearUsuario_ConCorreoYaRegistrado_LanzaDuplicado()
    {
        GivenRole(2, Rol());
        _store.Setup(s => s.EmailExistsAsync("ana@ordersync.local", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateEmailException>(() => Create().CreateUserAsync(AdminId, Request()));

        VerifyNothingWritten();
    }

    // ---------- Asignar rol ----------

    [Fact]
    public async Task AsignarRol_CambiaElRol_YRegistraLaBitacora()
    {
        GivenUser(10, Usuario(rolId: 3));
        GivenRole(2, Rol(2));

        var result = await Create().AssignRoleAsync(AdminId, 10, 2);

        _store.Verify(s => s.ChangeRoleAsync(AdminId, 10, 3, 2, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, result.RolId);
        Assert.Equal("Cajero", result.RolNombre);
    }

    [Fact]
    public async Task AsignarRol_UsuarioInexistente_LanzaNotFound()
    {
        GivenUser(10, null);
        GivenRole(2, Rol());

        await Assert.ThrowsAsync<SecurityNotFoundException>(() => Create().AssignRoleAsync(AdminId, 10, 2));

        VerifyNothingWritten();
    }

    [Fact]
    public async Task AsignarRol_RolInexistente_LanzaNotFound()
    {
        GivenUser(10, Usuario());
        GivenRole(2, null);

        await Assert.ThrowsAsync<SecurityNotFoundException>(() => Create().AssignRoleAsync(AdminId, 10, 2));

        VerifyNothingWritten();
    }

    [Fact]
    public async Task AsignarRol_RolDesactivado_LanzaReglaDeNegocio()
    {
        GivenUser(10, Usuario());
        GivenRole(2, Rol(activo: false));

        await Assert.ThrowsAsync<SecurityRuleException>(() => Create().AssignRoleAsync(AdminId, 10, 2));

        VerifyNothingWritten();
    }

    [Fact]
    public async Task AsignarRol_ElMismoRolQueYaTiene_LanzaReglaDeNegocio()
    {
        GivenUser(10, Usuario(rolId: 2));
        GivenRole(2, Rol(2));

        await Assert.ThrowsAsync<SecurityRuleException>(() => Create().AssignRoleAsync(AdminId, 10, 2));

        VerifyNothingWritten();
    }

    [Fact]
    public async Task AsignarRol_AUnoMismo_LanzaReglaDeNegocio()
    {
        GivenUser(AdminId, Usuario(AdminId, rolId: 5));
        GivenRole(2, Rol(2));

        await Assert.ThrowsAsync<SecurityRuleException>(() => Create().AssignRoleAsync(AdminId, AdminId, 2));

        VerifyNothingWritten();
    }

    [Fact]
    public void Constructor_ConDependenciaNula_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new UserAdministrationService(null!, _hasher.Object));
        Assert.Throws<ArgumentNullException>(() => new UserAdministrationService(_store.Object, null!));
    }
}

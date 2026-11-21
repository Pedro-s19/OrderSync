namespace OrderSync.Application.Seguridad;

public interface IUserAccountReader
{
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenGenerator
{
    IssuedToken Generate(TokenUser user);
}

/// <summary>Permiso actual del usuario sobre un módulo (se consulta en cada petición).</summary>
public interface IPermissionReader
{
    Task<PermissionInfo?> GetAsync(int usuarioId, string moduloCodigo, CancellationToken ct = default);
}

public interface IUserAdminStore
{
    Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken ct = default);
    Task<UserSummary?> GetUserAsync(int usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken ct = default);
    Task<RoleSummary?> GetRoleAsync(int rolId, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    /// <summary>Crea el usuario y registra la bitácora (ASIGNAR) en la misma operación.</summary>
    Task<UserSummary> CreateUserAsync(int adminId, NewUser user, CancellationToken ct = default);

    /// <summary>Cambia el rol y registra la bitácora (CAMBIAR) en la misma operación.</summary>
    Task ChangeRoleAsync(int adminId, int usuarioId, int rolAnteriorId, int rolNuevoId, CancellationToken ct = default);

    Task<IReadOnlyList<RoleAuditEntry>> ListAuditAsync(int? usuarioId, CancellationToken ct = default);
}

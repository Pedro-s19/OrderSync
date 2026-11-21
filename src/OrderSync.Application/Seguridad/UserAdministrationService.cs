using System.Net.Mail;

namespace OrderSync.Application.Seguridad;

/// <summary>
/// Creación de usuarios y asignación de roles. El control "solo el administrador"
/// lo aplica el filtro de autorización de la API (módulo USUARIOS, nivel total).
/// Cada cambio de rol queda en la bitácora (NFR-10).
/// </summary>
public sealed class UserAdministrationService
{
    private readonly IUserAdminStore _store;
    private readonly IPasswordHasher _hasher;

    public UserAdministrationService(IUserAdminStore store, IPasswordHasher hasher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    public Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken ct = default)
        => _store.ListUsersAsync(ct);

    public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken ct = default)
        => _store.ListRolesAsync(ct);

    public Task<IReadOnlyList<RoleAuditEntry>> ListAuditAsync(int? usuarioId, CancellationToken ct = default)
        => _store.ListAuditAsync(usuarioId, ct);

    public async Task<UserSummary> CreateUserAsync(
        int adminId, CreateUserRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Nombre) || request.Nombre.Trim().Length > 100)
            throw new SecurityValidationException("El nombre es obligatorio y admite hasta 100 caracteres.");
        if (!IsValidEmail(request.Email))
            throw new SecurityValidationException("El correo no es válido.");
        PasswordRules.Validate(request.Password);

        var role = await _store.GetRoleAsync(request.RolId, ct)
                   ?? throw new SecurityNotFoundException($"No existe el rol {request.RolId}.");
        if (!role.Activo)
            throw new SecurityRuleException($"El rol {role.Nombre} está desactivado.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _store.EmailExistsAsync(email, ct))
            throw new DuplicateEmailException(email);

        var user = new NewUser(request.Nombre.Trim(), email, _hasher.Hash(request.Password), request.RolId);
        return await _store.CreateUserAsync(adminId, user, ct);
    }

    public async Task<UserSummary> AssignRoleAsync(
        int adminId, int usuarioId, int rolId, CancellationToken ct = default)
    {
        var user = await _store.GetUserAsync(usuarioId, ct)
                   ?? throw new SecurityNotFoundException($"No existe el usuario {usuarioId}.");
        var role = await _store.GetRoleAsync(rolId, ct)
                   ?? throw new SecurityNotFoundException($"No existe el rol {rolId}.");

        if (!role.Activo)
            throw new SecurityRuleException($"El rol {role.Nombre} está desactivado.");
        if (usuarioId == adminId)
            throw new SecurityRuleException("Un administrador no puede cambiar su propio rol.");
        if (user.RolId == rolId)
            throw new SecurityRuleException($"El usuario ya tiene el rol {role.Nombre}.");

        await _store.ChangeRoleAsync(adminId, usuarioId, user.RolId, rolId, ct);
        return user with { RolId = role.RolId, RolNombre = role.Nombre };
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 150) return false;
        var trimmed = email.Trim();
        return MailAddress.TryCreate(trimmed, out var address) && address.Address == trimmed;
    }
}

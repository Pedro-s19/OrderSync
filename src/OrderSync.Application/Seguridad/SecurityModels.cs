namespace OrderSync.Application.Seguridad;

/// <summary>Niveles de la columna seguridad.Permiso.Nivel.</summary>
public static class PermissionLevel
{
    public const int SinAcceso = 0;
    public const int Lectura = 1;
    public const int Parcial = 2;
    public const int Total = 3;
}

/// <summary>
/// Política central de contraseñas para creación y cambio de credenciales.
/// El inicio de sesión no debe exigir retroactivamente la política nueva
/// a cuentas antiguas; solo valida que se hayan enviado credenciales.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            throw new SecurityValidationException($"La contraseña debe tener al menos {MinLength} caracteres.");

        if (!password.Any(char.IsUpper))
            throw new SecurityValidationException("La contraseña debe incluir al menos una letra mayúscula.");

        if (!password.Any(char.IsLower))
            throw new SecurityValidationException("La contraseña debe incluir al menos una letra minúscula.");

        if (!password.Any(char.IsDigit))
            throw new SecurityValidationException("La contraseña debe incluir al menos un número.");

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            throw new SecurityValidationException("La contraseña debe incluir al menos un carácter especial.");
    }
}

// ---- Login ----
public sealed record UserAccount(
    int UsuarioId, string Nombre, string Email, string PasswordHash,
    int RolId, string RolNombre, bool Activo, bool RolActivo);

public sealed record LoginRequest(string Email, string Password, string Rol);

public sealed record LoginResult(string Token, DateTime ExpiraUtc, int UsuarioId, string Nombre, string Rol);

public sealed record TokenUser(int UsuarioId, string Nombre, string Email, int RolId, string RolNombre);

public sealed record IssuedToken(string Token, DateTime ExpiraUtc);

// ---- Autorización por módulo ----
public sealed record PermissionInfo(bool UsuarioActivo, bool RolActivo, int Nivel, string? Alcance);

public sealed record AccessDecision(bool Allowed, string? Reason)
{
    public static AccessDecision Allow() => new(true, null);
    public static AccessDecision Deny(string reason) => new(false, reason);
}

// ---- Administración de usuarios y roles ----
public sealed record UserSummary(int UsuarioId, string Nombre, string Email, int RolId, string RolNombre, bool Activo);

public sealed record RoleSummary(int RolId, string Nombre, bool Activo);

public sealed record CreateUserRequest(string Nombre, string Email, string Password, int RolId);

public sealed record AssignRoleRequest(int RolId);

public sealed record NewUser(string Nombre, string Email, string PasswordHash, int RolId);

public sealed record RoleAuditEntry(
    long BitacoraId, int AdminUsuarioId, string Admin,
    int UsuarioAfectadoId, string UsuarioAfectado,
    string? RolAnterior, string? RolNuevo, string Accion, DateTime Fecha);

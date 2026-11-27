namespace OrderSync.Web.Security;

/// <summary>
/// Centraliza las comprobaciones de roles utilizadas por los controladores MVC.
/// La API continúa siendo la autoridad final para validar los permisos.
/// </summary>
public static class RoleAccess
{
    public const string SystemAdministrator = "Administrador del Sistema";

    public static bool IsAdministrator(string? role) =>
        string.Equals(role, SystemAdministrator, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// El administrador del sistema puede acceder a todos los módulos.
    /// Los demás roles deben estar incluidos explícitamente en la lista.
    /// </summary>
    public static bool Allows(string? role, params string[] allowedRoles) =>
        IsAdministrator(role) || allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

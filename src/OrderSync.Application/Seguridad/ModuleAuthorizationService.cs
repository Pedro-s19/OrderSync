namespace OrderSync.Application.Seguridad;

/// <summary>
/// Decide si un usuario puede acceder a un módulo, leyendo la matriz de seguridad.Permiso.
/// Nivel 0 = sin acceso, 1 = lectura, 2 = parcial (con alcance), 3 = total.
/// </summary>
public sealed class ModuleAuthorizationService
{
    private readonly IPermissionReader _permissions;

    public ModuleAuthorizationService(IPermissionReader permissions)
        => _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));

    /// <param name="alcanceContiene">Con acceso parcial, el alcance del permiso debe contener este texto.</param>
    /// <param name="alcanceNoContiene">Con acceso parcial, el alcance del permiso NO debe contener este texto
    /// (sirve para dejar fuera a quien solo ve "su asistencia" o solo "cobra").</param>
    public async Task<AccessDecision> CheckAsync(
        int usuarioId, string moduloCodigo, int nivelMinimo,
        string? alcanceContiene = null, CancellationToken ct = default,
        string? alcanceNoContiene = null)
    {
        var permiso = await _permissions.GetAsync(usuarioId, moduloCodigo, ct);

        if (permiso is null)
            return AccessDecision.Deny("Usuario o módulo no reconocido.");
        if (!permiso.UsuarioActivo)
            return AccessDecision.Deny("La cuenta está desactivada.");
        if (!permiso.RolActivo)
            return AccessDecision.Deny("El rol está desactivado.");
        if (permiso.Nivel < nivelMinimo)
            return AccessDecision.Deny($"Su rol no tiene permiso suficiente en {moduloCodigo}.");

        // El acceso total (nivel 3) cubre cualquier alcance; el parcial se compara contra el texto del permiso.
        if (permiso.Nivel < PermissionLevel.Total)
        {
            if (!string.IsNullOrWhiteSpace(alcanceContiene))
            {
                var cubre = permiso.Alcance is not null
                    && permiso.Alcance.Contains(alcanceContiene, StringComparison.OrdinalIgnoreCase);
                if (!cubre)
                    return AccessDecision.Deny($"Su acceso a {moduloCodigo} no cubre esta operación.");
            }

            if (!string.IsNullOrWhiteSpace(alcanceNoContiene)
                && permiso.Alcance is not null
                && permiso.Alcance.Contains(alcanceNoContiene, StringComparison.OrdinalIgnoreCase))
            {
                return AccessDecision.Deny($"Su acceso a {moduloCodigo} no cubre esta operación.");
            }
        }

        return AccessDecision.Allow();
    }
}

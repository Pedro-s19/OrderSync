using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Seguridad;

namespace OrderSync.Infrastructure.Persistence;

/// <summary>
/// Lee el permiso vigente en cada petición (no del token), así un cambio de rol
/// o una cuenta desactivada surten efecto de inmediato.
/// </summary>
public sealed class PermissionReader : IPermissionReader
{
    private readonly OrderSyncDbContext _db;

    public PermissionReader(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<PermissionInfo?> GetAsync(int usuarioId, string moduloCodigo, CancellationToken ct = default)
    {
        var user = await _db.Usuarios.AsNoTracking()
            .Where(u => u.UsuarioId == usuarioId)
            .Select(u => new { u.Activo, u.RolId, RolActivo = u.Rol.Activo })
            .FirstOrDefaultAsync(ct);
        if (user is null) return null;

        var permiso = await _db.Permisos.AsNoTracking()
            .Where(p => p.RolId == user.RolId && p.Modulo.Codigo == moduloCodigo)
            .Select(p => new { p.Nivel, p.Alcance })
            .FirstOrDefaultAsync(ct);
        if (permiso is null) return null;

        return new PermissionInfo(user.Activo, user.RolActivo, permiso.Nivel, permiso.Alcance);
    }
}

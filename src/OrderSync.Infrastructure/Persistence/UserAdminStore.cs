using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Seguridad;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class UserAdminStore : IUserAdminStore
{
    private const string AccionAsignar = "ASIGNAR";
    private const string AccionCambiar = "CAMBIAR";

    private readonly OrderSyncDbContext _db;

    public UserAdminStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken ct = default)
        => await _db.Usuarios.AsNoTracking()
            .OrderBy(u => u.UsuarioId)
            .Select(u => new UserSummary(u.UsuarioId, u.Nombre, u.Email, u.RolId, u.Rol.Nombre, u.Activo))
            .ToListAsync(ct);

    public Task<UserSummary?> GetUserAsync(int usuarioId, CancellationToken ct = default)
        => _db.Usuarios.AsNoTracking()
            .Where(u => u.UsuarioId == usuarioId)
            .Select(u => new UserSummary(u.UsuarioId, u.Nombre, u.Email, u.RolId, u.Rol.Nombre, u.Activo))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken ct = default)
        => await _db.Rols.AsNoTracking()
            .OrderBy(r => r.RolId)
            .Select(r => new RoleSummary(r.RolId, r.Nombre, r.Activo))
            .ToListAsync(ct);

    public Task<RoleSummary?> GetRoleAsync(int rolId, CancellationToken ct = default)
        => _db.Rols.AsNoTracking()
            .Where(r => r.RolId == rolId)
            .Select(r => new RoleSummary(r.RolId, r.Nombre, r.Activo))
            .FirstOrDefaultAsync(ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => _db.Usuarios.AnyAsync(u => u.Email == email, ct);

    public async Task<UserSummary> CreateUserAsync(int adminId, NewUser user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var rolNombre = await _db.Rols.AsNoTracking()
            .Where(r => r.RolId == user.RolId)
            .Select(r => r.Nombre)
            .FirstAsync(ct);

        var entity = new Usuario
        {
            Nombre = user.Nombre,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            RolId = user.RolId,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        _db.Usuarios.Add(entity);

        // Usuario y bitácora se guardan en un solo SaveChanges (atómico).
        _db.BitacoraRols.Add(new BitacoraRol
        {
            AdminUsuarioId = adminId,
            UsuarioAfectado = entity,
            RolAnteriorId = null,
            RolNuevoId = user.RolId,
            Accion = AccionAsignar,
            Fecha = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        return new UserSummary(entity.UsuarioId, entity.Nombre, entity.Email, entity.RolId, rolNombre, entity.Activo);
    }

    public async Task ChangeRoleAsync(
        int adminId, int usuarioId, int rolAnteriorId, int rolNuevoId, CancellationToken ct = default)
    {
        var entity = await _db.Usuarios.FirstAsync(u => u.UsuarioId == usuarioId, ct);
        entity.RolId = rolNuevoId;

        _db.BitacoraRols.Add(new BitacoraRol
        {
            AdminUsuarioId = adminId,
            UsuarioAfectadoId = usuarioId,
            RolAnteriorId = rolAnteriorId,
            RolNuevoId = rolNuevoId,
            Accion = AccionCambiar,
            Fecha = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RoleAuditEntry>> ListAuditAsync(int? usuarioId, CancellationToken ct = default)
        => await _db.BitacoraRols.AsNoTracking()
            .Where(b => usuarioId == null || b.UsuarioAfectadoId == usuarioId)
            .OrderByDescending(b => b.Fecha)
            .Select(b => new RoleAuditEntry(
                b.BitacoraId,
                b.AdminUsuarioId,
                b.AdminUsuario.Nombre,
                b.UsuarioAfectadoId,
                b.UsuarioAfectado.Nombre,
                b.RolAnterior == null ? null : b.RolAnterior.Nombre,
                b.RolNuevo == null ? null : b.RolNuevo.Nombre,
                b.Accion,
                b.Fecha))
            .ToListAsync(ct);
}

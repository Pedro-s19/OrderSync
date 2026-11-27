using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Seguridad;

namespace OrderSync.Infrastructure.Persistence;

public sealed class UserAccountReader : IUserAccountReader
{
    private readonly OrderSyncDbContext _db;

    public UserAccountReader(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct = default)
        => _db.Usuarios.AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => new UserAccount(
                u.UsuarioId, u.Nombre, u.Email, u.PasswordHash,
                u.RolId, u.Rol.Nombre, u.Activo, u.Rol.Activo))
            .FirstOrDefaultAsync(ct);
}

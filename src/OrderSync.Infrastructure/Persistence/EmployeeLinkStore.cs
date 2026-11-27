using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Rrhh;

namespace OrderSync.Infrastructure.Persistence;

public sealed class EmployeeLinkStore : IEmployeeLinkStore
{
    private readonly OrderSyncDbContext _db;

    public EmployeeLinkStore(OrderSyncDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<int?> GetEmployeeIdAsync(int usuarioId, CancellationToken ct = default)
        => _db.Usuarios.AsNoTracking()
            .Where(u => u.UsuarioId == usuarioId)
            .Select(u => u.EmpleadoId)
            .FirstOrDefaultAsync(ct);

    public Task<bool> UserExistsAsync(int usuarioId, CancellationToken ct = default)
        => _db.Usuarios.AnyAsync(u => u.UsuarioId == usuarioId, ct);

    public async Task LinkAsync(int usuarioId, int? empleadoId, CancellationToken ct = default)
    {
        var usuario = await _db.Usuarios.FirstAsync(u => u.UsuarioId == usuarioId, ct);
        usuario.EmpleadoId = empleadoId;
        await _db.SaveChangesAsync(ct);
    }
}


using Microsoft.EntityFrameworkCore;
using OrderSync.Application.Seguridad;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

public sealed class PasswordRecoveryStore : IPasswordRecoveryStore
{
    private readonly OrderSyncDbContext _db;

    public PasswordRecoveryStore(OrderSyncDbContext db)
    {
        _db = db;
    }

    public async Task<PasswordRecoveryUser?> FindActiveUserByEmailAsync(
        string email,
        CancellationToken ct = default)
    {
        return await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.Email == email && u.Activo)
            .Select(u => new PasswordRecoveryUser(
                u.UsuarioId,
                u.Email))
            .FirstOrDefaultAsync(ct);
    }

    public async Task InvalidatePendingAsync(
        int usuarioId,
        CancellationToken ct = default)
    {
        var recoveries = await _db.RecuperacionContrasenas
            .Where(r => r.UsuarioId == usuarioId && !r.Usado)
            .ToListAsync(ct);

        foreach (var recovery in recoveries)
        {
            recovery.Usado = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task CreateAsync(
        int usuarioId,
        string codigoHash,
        DateTime fechaExpiracion,
        CancellationToken ct = default)
    {
        var recovery = new RecuperacionContrasena
        {
            UsuarioId = usuarioId,
            CodigoHash = codigoHash,
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = fechaExpiracion,
            Usado = false,
            Intentos = 0
        };

        _db.RecuperacionContrasenas.Add(recovery);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<PasswordRecoveryRecord?> GetLatestAsync(
        int usuarioId,
        CancellationToken ct = default)
    {
        return await _db.RecuperacionContrasenas
            .AsNoTracking()
            .Where(r => r.UsuarioId == usuarioId)
            .OrderByDescending(r => r.FechaCreacion)
            .Select(r => new PasswordRecoveryRecord(
                r.RecuperacionId,
                r.UsuarioId,
                r.CodigoHash,
                r.FechaExpiracion,
                r.Usado,
                r.Intentos))
            .FirstOrDefaultAsync(ct);
    }

    public async Task IncrementAttemptsAsync(
        int recuperacionId,
        CancellationToken ct = default)
    {
        var recovery = await _db.RecuperacionContrasenas
            .FirstOrDefaultAsync(
                r => r.RecuperacionId == recuperacionId,
                ct);

        if (recovery is null)
            return;

        recovery.Intentos++;

        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAsUsedAsync(
        int recuperacionId,
        CancellationToken ct = default)
    {
        var recovery = await _db.RecuperacionContrasenas
            .FirstOrDefaultAsync(
                r => r.RecuperacionId == recuperacionId,
                ct);

        if (recovery is null)
            return;

        recovery.Usado = true;

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdatePasswordHashAsync(
        int usuarioId,
        string passwordHash,
        CancellationToken ct = default)
    {
        var user = await _db.Usuarios
            .FirstOrDefaultAsync(
                u => u.UsuarioId == usuarioId,
                ct);

        if (user is null)
            return;

        user.PasswordHash = passwordHash;

        await _db.SaveChangesAsync(ct);
    }
}

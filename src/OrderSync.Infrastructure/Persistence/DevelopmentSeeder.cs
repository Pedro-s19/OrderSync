using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderSync.Application.Seguridad;
using OrderSync.Infrastructure.Persistence.Entities;

namespace OrderSync.Infrastructure.Persistence;

/// <summary>
/// Solo para desarrollo: crea una cuenta de prueba por rol si no existen, para poder
/// iniciar sesión y probar permisos. La contraseña sale de Seed:Password.
/// </summary>
public static class DevelopmentSeeder
{
    private static readonly (string Email, string Nombre, string Rol)[] Cuentas =
    [
        ("admin@ordersync.local",      "Administrador de prueba", "Administrador del Sistema"),
        ("cajero@ordersync.local",     "Cajero de prueba",        "Cajero"),
        ("cocina@ordersync.local",     "Cocina de prueba",        "Cocina"),
        ("supervisor@ordersync.local", "Supervisor de prueba",    "Supervisor"),
        ("gerencia@ordersync.local",   "Gerencia de prueba",      "Gerencia")
    ];

    public static async Task SeedUsersAsync(
        IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        var password = configuration["Seed:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < PasswordRules.MinLength)
            return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderSyncDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var roles = await db.Rols.ToDictionaryAsync(r => r.Nombre, r => r.RolId, ct);
        string? hash = null;

        foreach (var (email, nombre, rol) in Cuentas)
        {
            if (!roles.TryGetValue(rol, out var rolId)) continue;
            if (await db.Usuarios.AnyAsync(u => u.Email == email, ct)) continue;

            hash ??= hasher.Hash(password);
            db.Usuarios.Add(new Usuario
            {
                Nombre = nombre,
                Email = email,
                PasswordHash = hash,
                RolId = rolId,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}


using System.Security.Cryptography;
using System.Text;

namespace OrderSync.Application.Seguridad;

public sealed class PasswordRecoveryService
{
    private const int CodeLength = 6;
    private const int CodeExpirationMinutes = 10;
    private const int MaxAttempts = 5;

    private readonly IPasswordRecoveryStore _store;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _emailSender;

    public PasswordRecoveryService(
        IPasswordRecoveryStore store,
        IPasswordHasher hasher,
        IEmailSender emailSender)
    {
        _store = store;
        _hasher = hasher;
        _emailSender = emailSender;
    }

    public async Task RequestCodeAsync(
        string email,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new SecurityValidationException(
                "Ingresa tu correo electrónico.");

        email = email.Trim().ToLowerInvariant();

        var user = await _store.FindActiveUserByEmailAsync(email, ct);

        // No revelar si el correo existe.
        if (user is null)
            return;

        await _store.InvalidatePendingAsync(user.UsuarioId, ct);

        var code = GenerateCode();
        var codeHash = HashCode(code);

        var expiration = DateTime.UtcNow.AddMinutes(CodeExpirationMinutes);

        await _store.CreateAsync(
            user.UsuarioId,
            codeHash,
            expiration,
            ct);

        var subject = "Recuperación de contraseña - OrderSync";

        var body = $"""
            Hola,

            Recibimos una solicitud para recuperar tu contraseña de OrderSync.

            Tu código de verificación es: {code}

            Este código vence en 10 minutos y solo puede utilizarse una vez.

            Si no solicitaste este cambio, puedes ignorar este correo.

            Equipo OrderSync
            """;

        await _emailSender.SendAsync(
            user.Email,
            subject,
            body,
            ct);
    }

    public async Task ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new SecurityValidationException(
                "Ingresa tu correo electrónico.");

        if (string.IsNullOrWhiteSpace(code))
            throw new SecurityValidationException(
                "Ingresa el código de recuperación.");

        if (string.IsNullOrWhiteSpace(newPassword))
            throw new SecurityValidationException(
                "Ingresa la nueva contraseña.");

        email = email.Trim().ToLowerInvariant();
        code = code.Trim();

        if (code.Length != CodeLength ||
            !code.All(char.IsDigit))
        {
            throw new SecurityValidationException(
                "El código de recuperación no es válido.");
        }

        var user = await _store.FindActiveUserByEmailAsync(email, ct);

        if (user is null)
            throw new SecurityValidationException(
                "El código de recuperación no es válido o ha expirado.");

        var recovery = await _store.GetLatestAsync(user.UsuarioId, ct);

        if (recovery is null ||
            recovery.Usado ||
            recovery.FechaExpiracion <= DateTime.UtcNow ||
            recovery.Intentos >= MaxAttempts)
        {
            throw new SecurityValidationException(
                "El código de recuperación no es válido o ha expirado.");
        }

        var suppliedHash = HashCode(code);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(suppliedHash),
            Encoding.UTF8.GetBytes(recovery.CodigoHash)))
        {
            await _store.IncrementAttemptsAsync(
                recovery.RecuperacionId,
                ct);

            throw new SecurityValidationException(
                "El código de recuperación no es válido.");
        }

        PasswordRules.Validate(newPassword);

        var passwordHash = _hasher.Hash(newPassword);

        await _store.UpdatePasswordHashAsync(
            user.UsuarioId,
            passwordHash,
            ct);

        await _store.MarkAsUsedAsync(
            recovery.RecuperacionId,
            ct);
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6");
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));

        return Convert.ToHexString(bytes);
    }
}

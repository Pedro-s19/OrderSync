namespace OrderSync.Application.Seguridad;

/// <summary>
/// Inicio de sesión: contraseña de 8+ caracteres y el rol elegido debe coincidir
/// con el de la cuenta. Todos los fallos de credenciales devuelven el mismo error.
/// </summary>
public sealed class AuthService
{
    private readonly IUserAccountReader _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenGenerator _tokens;

    public AuthService(IUserAccountReader users, IPasswordHasher hasher, ITokenGenerator tokens)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Rol))
            throw new SecurityValidationException("El correo y el rol son obligatorios.");
        PasswordRules.Validate(request.Password);

        var email = request.Email.Trim().ToLowerInvariant();
        var account = await _users.FindByEmailAsync(email, ct);

        if (account is null
            || !account.Activo
            || !account.RolActivo
            || !_hasher.Verify(request.Password, account.PasswordHash)
            || !string.Equals(account.RolNombre, request.Rol.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidCredentialsException();
        }

        var token = _tokens.Generate(new TokenUser(
            account.UsuarioId, account.Nombre, account.Email, account.RolId, account.RolNombre));

        return new LoginResult(token.Token, token.ExpiraUtc, account.UsuarioId, account.Nombre, account.RolNombre);
    }
}

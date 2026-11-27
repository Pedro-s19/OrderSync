
namespace OrderSync.Application.Seguridad;

/// <summary>
/// Servicio encargado de autenticar a los usuarios.
///
/// Comprueba:
/// - Que el correo y la contraseña estén informados.
/// - Que la cuenta esté activa.
/// - Que la contraseña sea correcta.
/// - Que el rol seleccionado corresponda a la cuenta.
///
/// Las reglas de contraseña fuerte se aplican al crear
/// o cambiar contraseñas, no al iniciar sesión.
/// </summary>
public sealed class AuthService
{
    private readonly IUserAccountReader _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenGenerator _tokens;

    public AuthService(
        IUserAccountReader users,
        IPasswordHasher hasher,
        ITokenGenerator tokens)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    /// <summary>
    /// Realiza el inicio de sesión del usuario.
    /// </summary>
    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validar que los campos obligatorios estén completos.
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new SecurityValidationException(
                "Ingresa tu correo electrónico.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new SecurityValidationException(
                "Ingresa tu contraseña.");

        if (string.IsNullOrWhiteSpace(request.Rol))
            throw new SecurityValidationException(
                "Selecciona tu rol para continuar.");

        // 2. Normalizar el correo electrónico.
        var email = request.Email.Trim().ToLowerInvariant();

        // 3. Buscar la cuenta en la base de datos.
        var account = await _users.FindByEmailAsync(email, ct);

        // 4. Verificar que la cuenta exista y esté activa.
        // No revelamos si el correo está registrado.
        if (account is null || !account.Activo || !account.RolActivo)
        {
            throw new InvalidCredentialsException();
        }

        // 5. Comprobar la contraseña.
        // Si es incorrecta, se muestra un mensaje genérico.
        if (!_hasher.Verify(request.Password, account.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        // 6. Comprobar el rol después de validar la contraseña.
        // Así no revelamos información de la cuenta a quien
        // solamente conoce el correo electrónico.
        if (!string.Equals(
            account.RolNombre,
            request.Rol.Trim(),
            StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityValidationException(
                "El rol seleccionado no corresponde a tu cuenta. Selecciona el rol correcto.");
        }

        // 7. Generar el token de autenticación.
        var token = _tokens.Generate(
            new TokenUser(
                account.UsuarioId,
                account.Nombre,
                account.Email,
                account.RolId,
                account.RolNombre
            )
        );

        // 8. Devolver los datos de la sesión.
        return new LoginResult(
            token.Token,
            token.ExpiraUtc,
            account.UsuarioId,
            account.Nombre,
            account.RolNombre
        );
    }
}

namespace OrderSync.Application.Seguridad;

/// <summary>Datos de entrada no válidos (400).</summary>
public sealed class SecurityValidationException(string message) : Exception(message);

/// <summary>Usuario o rol inexistente (404).</summary>
public sealed class SecurityNotFoundException(string message) : Exception(message);

/// <summary>Regla de negocio incumplida (422).</summary>
public sealed class SecurityRuleException(string message) : Exception(message);

/// <summary>Correo ya registrado (409).</summary>
public sealed class DuplicateEmailException(string email)
    : Exception($"Ya existe una cuenta con el correo {email}.");

/// <summary>Credenciales o rol incorrectos (401). Mensaje genérico a propósito.</summary>
public sealed class InvalidCredentialsException() : Exception("Credenciales no válidas.");

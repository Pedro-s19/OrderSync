namespace OrderSync.Application.Common;

/// <summary>Datos de entrada no válidos (HTTP 400).</summary>
public sealed class RequestValidationException(string message) : Exception(message);

/// <summary>El recurso pedido no existe (HTTP 404).</summary>
public sealed class ResourceNotFoundException(string message) : Exception(message);

/// <summary>El estado actual no permite la operación, o el dato ya existe (HTTP 409).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Regla de negocio incumplida (HTTP 422).</summary>
public sealed class BusinessRuleException(string message) : Exception(message);

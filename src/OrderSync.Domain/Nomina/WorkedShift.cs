namespace OrderSync.Domain.Nomina;

/// <summary>
/// Un turno trabajado según el registro de asistencia (FR-14).
/// Salida nula = turno sin cierre; TieneTurnoProgramado falso = asistencia sin turno.
/// </summary>
public sealed record WorkedShift(DateTime Entrada, DateTime? Salida, bool TieneTurnoProgramado = true);

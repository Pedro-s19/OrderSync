namespace OrderSync.Application.Common;

/// <summary>
/// Reloj de la aplicación. Turnos, asistencia y reportes usan la hora de Colombia;
/// las marcas de auditoría de la base siguen en UTC.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }

    /// <summary>Hora local de Colombia (UTC-5, sin horario de verano).</summary>
    DateTime LocalNow { get; }

    DateTime ToLocal(DateTime utc);
    DateTime ToUtc(DateTime local);
}

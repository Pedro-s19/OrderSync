namespace OrderSync.Application.Crm;

/// <summary>
/// Regla pura (FR-17): una promoción se aplica sola si está asignada al cliente, activa,
/// vigente hoy y el cliente cumple los puntos mínimos. Si hay varias, gana el mayor descuento.
/// </summary>
public static class PromotionPolicy
{
    public static PromotionDto? SelectBest(IEnumerable<PromotionDto> assigned, int puntos, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(assigned);

        return assigned
            .Where(p => p.Activa
                        && p.VigenteDesde <= hoy
                        && (p.VigenteHasta is null || p.VigenteHasta >= hoy)
                        && (p.PuntosMinimos is null || puntos >= p.PuntosMinimos))
            .OrderByDescending(p => p.PorcentajeDto)
            .ThenBy(p => p.PromocionId)
            .FirstOrDefault();
    }
}

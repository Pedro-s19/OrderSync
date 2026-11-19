namespace OrderSync.Domain.Nomina;

public static class Money
{
    /// <summary>Redondea a pesos enteros (mitad hacia arriba) para coincidir con el cálculo manual.</summary>
    public static decimal ToPesos(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}

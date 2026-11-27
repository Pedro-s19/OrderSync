namespace OrderSync.Domain.Nomina;

/// <summary>Error de validación de datos de nómina (la API lo traduce a 400).</summary>
public sealed class PayrollValidationException : Exception
{
    public PayrollValidationException(string message) : base(message) { }
}

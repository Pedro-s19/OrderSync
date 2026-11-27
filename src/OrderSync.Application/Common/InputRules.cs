using System.Net.Mail;

namespace OrderSync.Application.Common;

/// <summary>Validaciones de entrada compartidas por los servicios del CRUD.</summary>
public static class InputRules
{
    public const int MaxPageSize = 200;

    public static string Required(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RequestValidationException($"{field} es obligatorio.");
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new RequestValidationException($"{field} admite hasta {max} caracteres.");
        return trimmed;
    }

    public static string? Optional(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new RequestValidationException($"{field} admite hasta {max} caracteres.");
        return trimmed;
    }

    public static string? OptionalEmail(string? value, string field = "El correo")
    {
        var email = Optional(value, 150, field);
        if (email is null) return null;
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new RequestValidationException($"{field} no es válido.");
        return email.ToLowerInvariant();
    }

    public static void Paging(int pagina, int tamano)
    {
        if (pagina < 1 || tamano < 1 || tamano > MaxPageSize)
            throw new RequestValidationException(
                $"La página debe ser 1 o más y el tamaño entre 1 y {MaxPageSize}.");
    }

    public static void DateRange(DateTime? desde, DateTime? hasta)
    {
        if (desde > hasta)
            throw new RequestValidationException("La fecha 'desde' no puede ser posterior a 'hasta'.");
    }
}

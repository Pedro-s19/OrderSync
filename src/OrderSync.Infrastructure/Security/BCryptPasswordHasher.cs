using OrderSync.Application.Seguridad;
using BCryptNet = BCrypt.Net.BCrypt;

namespace OrderSync.Infrastructure.Security;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return BCryptNet.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;

        try
        {
            return BCryptNet.Verify(password, hash);
        }
        catch (Exception)
        {
            // Hash con formato inválido en la base: se trata como contraseña incorrecta.
            return false;
        }
    }
}


namespace OrderSync.Infrastructure.Persistence.Entities;

public class RecuperacionContrasena
{
    public int RecuperacionId { get; set; }

    public int UsuarioId { get; set; }

    public string CodigoHash { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateTime FechaExpiracion { get; set; }

    public bool Usado { get; set; }

    public int Intentos { get; set; }
}

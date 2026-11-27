using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Cliente
{
    public int ClienteId { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public string? Direccion { get; set; }

    public int PuntosFidelidad { get; set; }

    public DateTime FechaRegistro { get; set; }

    public bool Activo { get; set; }

    public virtual ICollection<MovimientoPunto> MovimientoPuntos { get; set; } = new List<MovimientoPunto>();

    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();

    public virtual ICollection<Promocion> Promocions { get; set; } = new List<Promocion>();
}

using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class MovimientoPunto
{
    public long MovimientoId { get; set; }

    public int ClienteId { get; set; }

    public int? PedidoId { get; set; }

    public int Puntos { get; set; }

    public DateTime Fecha { get; set; }

    public virtual Cliente Cliente { get; set; } = null!;

    public virtual Pedido? Pedido { get; set; }
}

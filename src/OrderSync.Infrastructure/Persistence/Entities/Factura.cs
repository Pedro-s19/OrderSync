using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Factura
{
    public int FacturaId { get; set; }

    public int PedidoId { get; set; }

    public DateTime Fecha { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Propina { get; set; }

    public decimal Total { get; set; }

    public virtual ICollection<Pago> Pagos { get; set; } = new List<Pago>();

    public virtual Pedido Pedido { get; set; } = null!;
}

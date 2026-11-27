using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Pago
{
    public int PagoId { get; set; }

    public int FacturaId { get; set; }

    public string Metodo { get; set; } = null!;

    public decimal Monto { get; set; }

    public DateTime Fecha { get; set; }

    public virtual Factura Factura { get; set; } = null!;
}

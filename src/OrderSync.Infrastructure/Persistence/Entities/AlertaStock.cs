using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class AlertaStock
{
    public int AlertaId { get; set; }

    public int IngredienteId { get; set; }

    public decimal StockAlMomento { get; set; }

    public decimal StockMinimo { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime Fecha { get; set; }

    public DateTime? FechaAtencion { get; set; }

    public virtual Ingrediente Ingrediente { get; set; } = null!;

    public virtual ICollection<OrdenCompra> OrdenCompras { get; set; } = new List<OrdenCompra>();
}

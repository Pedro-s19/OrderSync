using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class DetalleOrdenCompra
{
    public int DetalleId { get; set; }

    public int OrdenCompraId { get; set; }

    public int IngredienteId { get; set; }

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal CantidadRecibida { get; set; }

    public virtual Ingrediente Ingrediente { get; set; } = null!;

    public virtual OrdenCompra OrdenCompra { get; set; } = null!;
}

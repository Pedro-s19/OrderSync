using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class ProveedorIngrediente
{
    public int ProveedorId { get; set; }

    public int IngredienteId { get; set; }

    public decimal PrecioUnitario { get; set; }

    public DateOnly FechaPrecio { get; set; }

    public virtual Ingrediente Ingrediente { get; set; } = null!;

    public virtual Proveedor Proveedor { get; set; } = null!;
}

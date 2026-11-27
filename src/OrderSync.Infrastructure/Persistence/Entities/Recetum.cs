using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Recetum
{
    public int ProductoId { get; set; }

    public int IngredienteId { get; set; }

    public decimal Cantidad { get; set; }

    public virtual Ingrediente Ingrediente { get; set; } = null!;

    public virtual Producto Producto { get; set; } = null!;
}

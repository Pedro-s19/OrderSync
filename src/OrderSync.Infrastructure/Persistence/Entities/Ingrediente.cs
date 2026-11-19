using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Ingrediente
{
    public int IngredienteId { get; set; }

    public string Nombre { get; set; } = null!;

    public string UnidadMedida { get; set; } = null!;

    public decimal StockActual { get; set; }

    public decimal StockMinimo { get; set; }

    public bool Activo { get; set; }

    public virtual ICollection<AlertaStock> AlertaStocks { get; set; } = new List<AlertaStock>();

    public virtual ICollection<DetalleOrdenCompra> DetalleOrdenCompras { get; set; } = new List<DetalleOrdenCompra>();

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();

    public virtual ICollection<ProveedorIngrediente> ProveedorIngredientes { get; set; } = new List<ProveedorIngrediente>();

    public virtual ICollection<Recetum> Receta { get; set; } = new List<Recetum>();
}

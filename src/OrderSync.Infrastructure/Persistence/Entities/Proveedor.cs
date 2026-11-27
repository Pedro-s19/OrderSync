using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Proveedor
{
    public int ProveedorId { get; set; }

    public string Nombre { get; set; } = null!;

    public string Contacto { get; set; } = null!;

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public bool Activo { get; set; }

    public virtual ICollection<OrdenCompra> OrdenCompras { get; set; } = new List<OrdenCompra>();

    public virtual ICollection<ProveedorIngrediente> ProveedorIngredientes { get; set; } = new List<ProveedorIngrediente>();
}

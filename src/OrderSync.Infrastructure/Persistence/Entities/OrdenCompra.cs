using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class OrdenCompra
{
    public int OrdenCompraId { get; set; }

    public int ProveedorId { get; set; }

    public int? UsuarioId { get; set; }

    public int? AlertaOrigenId { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateOnly? FechaEsperada { get; set; }

    public virtual AlertaStock? AlertaOrigen { get; set; }

    public virtual ICollection<DetalleOrdenCompra> DetalleOrdenCompras { get; set; } = new List<DetalleOrdenCompra>();

    public virtual Proveedor Proveedor { get; set; } = null!;

    public virtual ICollection<RecepcionMercancium> RecepcionMercancia { get; set; } = new List<RecepcionMercancium>();

    public virtual Usuario? Usuario { get; set; }
}

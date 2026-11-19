using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class RecepcionMercancium
{
    public int RecepcionId { get; set; }

    public int OrdenCompraId { get; set; }

    public int? UsuarioId { get; set; }

    public DateTime Fecha { get; set; }

    public bool? EntregaAtiempo { get; set; }

    public string? Observaciones { get; set; }

    public virtual OrdenCompra OrdenCompra { get; set; } = null!;

    public virtual Usuario? Usuario { get; set; }
}

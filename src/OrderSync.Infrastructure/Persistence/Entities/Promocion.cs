using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Promocion
{
    public int PromocionId { get; set; }

    public string Nombre { get; set; } = null!;

    public decimal PorcentajeDto { get; set; }

    public int? PuntosMinimos { get; set; }

    public DateOnly VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public bool Activa { get; set; }

    public virtual ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
}

using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class LiquidacionNomina
{
    public int LiquidacionId { get; set; }

    public int EmpleadoId { get; set; }

    public int? UsuarioId { get; set; }

    public int Anio { get; set; }

    public byte Mes { get; set; }

    public decimal HorasOrdinarias { get; set; }

    public decimal HorasExtra { get; set; }

    public decimal PagoBruto { get; set; }

    public decimal Salud { get; set; }

    public decimal Pension { get; set; }

    public decimal Solidaridad { get; set; }

    public decimal AuxilioTransporte { get; set; }

    public decimal Neto { get; set; }

    public DateTime FechaCalculo { get; set; }

    public virtual Empleado Empleado { get; set; } = null!;

    public virtual Usuario? Usuario { get; set; }
}

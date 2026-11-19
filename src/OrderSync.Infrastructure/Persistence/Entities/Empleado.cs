using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Empleado
{
    public int EmpleadoId { get; set; }

    public string Documento { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string AreaCargo { get; set; } = null!;

    public string TipoContrato { get; set; } = null!;

    public decimal TarifaHora { get; set; }

    public decimal? SalarioMensual { get; set; }

    public DateOnly FechaIngreso { get; set; }

    public bool Activo { get; set; }

    public virtual ICollection<LiquidacionNomina> LiquidacionNominas { get; set; } = new List<LiquidacionNomina>();

    public virtual ICollection<Turno> Turnos { get; set; } = new List<Turno>();

    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}

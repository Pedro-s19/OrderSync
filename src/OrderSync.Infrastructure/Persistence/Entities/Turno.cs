using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Turno
{
    public int TurnoId { get; set; }

    public int EmpleadoId { get; set; }

    public DateTime Inicio { get; set; }

    public DateTime Fin { get; set; }

    public string Estado { get; set; } = null!;

    public virtual ICollection<Asistencium> Asistencia { get; set; } = new List<Asistencium>();

    public virtual Empleado Empleado { get; set; } = null!;
}

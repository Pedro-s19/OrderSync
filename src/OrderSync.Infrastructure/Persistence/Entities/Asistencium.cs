using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Asistencium
{
    public int AsistenciaId { get; set; }

    public int TurnoId { get; set; }

    public DateTime Entrada { get; set; }

    public DateTime? Salida { get; set; }

    public virtual Turno Turno { get; set; } = null!;
}

using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Permiso
{
    public int RolId { get; set; }

    public int ModuloId { get; set; }

    public byte Nivel { get; set; }

    public string? Alcance { get; set; }

    public virtual Modulo Modulo { get; set; } = null!;

    public virtual Rol Rol { get; set; } = null!;
}

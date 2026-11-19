using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Rol
{
    public int RolId { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    public virtual ICollection<BitacoraRol> BitacoraRolRolAnteriors { get; set; } = new List<BitacoraRol>();

    public virtual ICollection<BitacoraRol> BitacoraRolRolNuevos { get; set; } = new List<BitacoraRol>();

    public virtual ICollection<Permiso> Permisos { get; set; } = new List<Permiso>();

    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}

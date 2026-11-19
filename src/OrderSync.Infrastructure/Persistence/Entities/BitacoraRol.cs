using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class BitacoraRol
{
    public long BitacoraId { get; set; }

    public int AdminUsuarioId { get; set; }

    public int UsuarioAfectadoId { get; set; }

    public int? RolAnteriorId { get; set; }

    public int? RolNuevoId { get; set; }

    public string Accion { get; set; } = null!;

    public DateTime Fecha { get; set; }

    public virtual Usuario AdminUsuario { get; set; } = null!;

    public virtual Rol? RolAnterior { get; set; }

    public virtual Rol? RolNuevo { get; set; }

    public virtual Usuario UsuarioAfectado { get; set; } = null!;
}

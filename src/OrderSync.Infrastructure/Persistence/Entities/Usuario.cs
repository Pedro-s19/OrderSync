using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Usuario
{
    public int UsuarioId { get; set; }

    public string Nombre { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public int RolId { get; set; }

    public int? EmpleadoId { get; set; }

    public bool Activo { get; set; }

    public DateTime FechaCreacion { get; set; }

    public virtual ICollection<BitacoraRol> BitacoraRolAdminUsuarios { get; set; } = new List<BitacoraRol>();

    public virtual ICollection<BitacoraRol> BitacoraRolUsuarioAfectados { get; set; } = new List<BitacoraRol>();

    public virtual Empleado? Empleado { get; set; }

    public virtual ICollection<LiquidacionNomina> LiquidacionNominas { get; set; } = new List<LiquidacionNomina>();

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();

    public virtual ICollection<OrdenCompra> OrdenCompras { get; set; } = new List<OrdenCompra>();

    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();

    public virtual ICollection<RecepcionMercancium> RecepcionMercancia { get; set; } = new List<RecepcionMercancium>();

    public virtual Rol Rol { get; set; } = null!;
}

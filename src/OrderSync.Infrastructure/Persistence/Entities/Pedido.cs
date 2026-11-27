using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class Pedido
{
    public int PedidoId { get; set; }

    public int? ClienteId { get; set; }

    public int? UsuarioId { get; set; }

    public string Canal { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public string? Mesa { get; set; }

    public string? DireccionEntrega { get; set; }

    public string? TelefonoContacto { get; set; }

    public decimal Total { get; set; }

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaEnvioCocina { get; set; }

    public DateTime? FechaListo { get; set; }

    public virtual Cliente? Cliente { get; set; }

    public virtual ICollection<DetallePedido> DetallePedidos { get; set; } = new List<DetallePedido>();

    public virtual Factura? Factura { get; set; }

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();

    public virtual ICollection<MovimientoPunto> MovimientoPuntos { get; set; } = new List<MovimientoPunto>();

    public virtual Usuario? Usuario { get; set; }
}

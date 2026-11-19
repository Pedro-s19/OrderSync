using System;
using System.Collections.Generic;

namespace OrderSync.Infrastructure.Persistence.Entities;

public partial class MovimientoInventario
{
    public long MovimientoId { get; set; }

    public int IngredienteId { get; set; }

    public string Tipo { get; set; } = null!;

    public decimal Cantidad { get; set; }

    public int? PedidoId { get; set; }

    public int? UsuarioId { get; set; }

    public string? Motivo { get; set; }

    public DateTime Fecha { get; set; }

    public virtual Ingrediente Ingrediente { get; set; } = null!;

    public virtual Pedido? Pedido { get; set; }

    public virtual Usuario? Usuario { get; set; }
}

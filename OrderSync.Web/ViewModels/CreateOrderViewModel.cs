using System.ComponentModel.DataAnnotations;
using OrderSync.Web.Services;

namespace OrderSync.Web.ViewModels;

public sealed class CreateOrderViewModel
{
    [Required] public string Canal { get; set; } = "MOSTRADOR";
    public string? Mesa { get; set; }
    public string? DireccionEntrega { get; set; }
    public string? TelefonoContacto { get; set; }
    public int? ClienteId { get; set; }
    public List<OrderLineForm> Lineas { get; set; } = [new()];
    public List<ProductDto> Productos { get; set; } = [];
}

public sealed class OrderLineForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un producto.")]
    public int ProductoId { get; set; }
    [Range(1, 1000, ErrorMessage = "La cantidad debe ser entre 1 y 1000.")]
    public int Cantidad { get; set; } = 1;
}

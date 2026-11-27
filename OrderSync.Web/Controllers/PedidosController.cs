using Microsoft.AspNetCore.Mvc;
using OrderSync.Web.Services;
using OrderSync.Web.ViewModels;
using OrderSync.Web.Security;

namespace OrderSync.Web.Controllers;

public class PedidosController : Controller
{
    private readonly OrderSyncApiClient _api;
    public PedidosController(OrderSyncApiClient api) => _api = api;

    private string? Token => HttpContext.Session.GetString("Token");
    private string Role => HttpContext.Session.GetString("Rol") ?? "";

    public async Task<IActionResult> Index()
    {
        if (Token is null) return RedirectToAction("Login", "Auth");
        if (Role == "Cocina") return RedirectToAction("Index", "Cocina");
        try
        {
            var data = await _api.GetAsync<List<OrderSummaryDto>>("api/pedidos?pagina=1&tamano=100", Token) ?? [];
            return View(data);
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; return View(new List<OrderSummaryDto>()); }
        catch (HttpRequestException) { TempData["Error"] = "No se pudo conectar con OrderSync API. Verifica que el backend esté iniciado."; return View(new List<OrderSummaryDto>()); }
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        if (Token is null) return RedirectToAction("Login", "Auth");
        if (!RoleAccess.Allows(Role, "Cajero")) return Forbid();
        try
        {
            var vm = new CreateOrderViewModel
            {
                Productos = (await _api.GetAsync<List<ProductDto>>("api/productos", Token) ?? [])
                    .Where(p => p.Activo).OrderBy(p => p.Nombre).ToList()
            };
            if (vm.Productos.Count == 0) TempData["Error"] = "No hay productos activos. Registra productos antes de crear un pedido.";
            return View(vm);
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; return View(new CreateOrderViewModel()); }
        catch (HttpRequestException) { TempData["Error"] = "No se pudo conectar con la API."; return View(new CreateOrderViewModel()); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CreateOrderViewModel vm)
    {
        if (Token is null) return RedirectToAction("Login", "Auth");
        if (!RoleAccess.Allows(Role, "Cajero")) return Forbid();
        vm.Lineas = vm.Lineas?.Where(x => x.ProductoId > 0 && x.Cantidad > 0).ToList() ?? [];
        if (vm.Lineas.Count == 0) ModelState.AddModelError("Lineas", "Agrega al menos un producto al pedido.");
        if (vm.Canal == "DOMICILIO" && (string.IsNullOrWhiteSpace(vm.DireccionEntrega) || string.IsNullOrWhiteSpace(vm.TelefonoContacto)))
            ModelState.AddModelError("DireccionEntrega", "Para domicilio debes indicar dirección y teléfono.");
        if (vm.Canal == "QR" && string.IsNullOrWhiteSpace(vm.Mesa))
            ModelState.AddModelError("Mesa", "Para pedidos QR debes indicar la mesa.");
        if (!ModelState.IsValid)
        {
            vm.Productos = await _api.GetAsync<List<ProductDto>>("api/productos", Token) ?? [];
            vm.Productos = vm.Productos.Where(p => p.Activo).OrderBy(p => p.Nombre).ToList();
            return View(vm);
        }

        var request = new OrderRequestDto(vm.Canal, vm.Mesa, vm.DireccionEntrega, vm.TelefonoContacto,
            vm.ClienteId, vm.Lineas.Select(x => new OrderLineDto(x.ProductoId, x.Cantidad)).ToList());
        try
        {
            var created = await _api.PostAsync<OrderRequestDto, OrderDetailDto>("api/pedidos", request, Token);
            if (created is null) throw new ApiException("La API no devolvió el pedido creado.", 500);
            TempData["Ok"] = $"Pedido #{created.PedidoId} creado en BORRADOR. Ahora puedes confirmarlo.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            vm.Productos = await _api.GetAsync<List<ProductDto>>("api/productos", Token) ?? [];
            vm.Productos = vm.Productos.Where(p => p.Activo).OrderBy(p => p.Nombre).ToList();
            return View(vm);
        }
        catch (HttpRequestException) { TempData["Error"] = "No se pudo conectar con la API."; return RedirectToAction(nameof(Crear)); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(int id)
    {
        if (Token is null) return RedirectToAction("Login", "Auth");
        if (!RoleAccess.Allows(Role, "Cajero")) return Forbid();
        try
        {
            await _api.PostAsync<object, System.Text.Json.JsonElement>($"api/pedidos/{id}/confirmar", new { }, Token);
            TempData["Ok"] = $"Pedido #{id} confirmado. El backend aplicó el descuento de inventario según la receta.";
        }
        catch (ApiException ex) { TempData["Error"] = $"No se pudo confirmar el pedido #{id}: {ex.Message}"; }
        catch (HttpRequestException) { TempData["Error"] = "No se pudo conectar con la API."; }
        return RedirectToAction(nameof(Index));
    }
}

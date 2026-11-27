using Microsoft.AspNetCore.Mvc;
using OrderSync.Web.Services;
using OrderSync.Web.Security;

namespace OrderSync.Web.Controllers;

public class CocinaController : Controller
{
    private readonly OrderSyncApiClient _api;
    public CocinaController(OrderSyncApiClient api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var token = HttpContext.Session.GetString("Token");
        if (token is null) return RedirectToAction("Login", "Auth");
        if (!RoleAccess.Allows(HttpContext.Session.GetString("Rol"), "Cocina")) return Forbid();
        try
        {
            var all = await _api.GetAsync<List<OrderSummaryDto>>("api/pedidos?pagina=1&tamano=100", token) ?? [];
            var active = all.Where(p => p.Estado is "CONFIRMADO" or "EN_COCINA")
                .OrderBy(p => p.FechaCreacion).ToList();
            var details = new List<OrderDetailDto>();
            foreach (var order in active)
            {
                var detail = await _api.GetAsync<OrderDetailDto>($"api/pedidos/{order.PedidoId}", token);
                if (detail is not null) details.Add(detail);
            }
            return View(details);
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; return View(new List<OrderDetailDto>()); }
        catch (HttpRequestException) { TempData["Error"] = "No se pudo conectar con OrderSync API."; return View(new List<OrderDetailDto>()); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarListo(int id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (token is null) return RedirectToAction("Login", "Auth");
        if (!RoleAccess.Allows(HttpContext.Session.GetString("Rol"), "Cocina")) return Forbid();
        try
        {
            await _api.PostAsync<object, OrderDetailDto>($"api/pedidos/{id}/listo", new { }, token);
            TempData["Ok"] = $"Pedido #{id} marcado como LISTO.";
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
        catch (HttpRequestException) { TempData["Error"] = "No se pudo conectar con la API."; }
        return RedirectToAction(nameof(Index));
    }
}

using Microsoft.AspNetCore.Mvc;
using OrderSync.Web.Services;

namespace OrderSync.Web.Controllers;

public class InventarioController : Controller
{
    private readonly OrderSyncApiClient _api;

    public InventarioController(OrderSyncApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index()
    {
        var token = HttpContext.Session.GetString("Token");
        if (token is null)
            return RedirectToAction("Login", "Auth");

        try
        {
            var alerts = await _api.GetAsync<List<StockAlertDto>>(
                "api/inventario/alertas",
                token
            ) ?? new List<StockAlertDto>();

            return View(alerts);
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return View(new List<StockAlertDto>());
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "No se pudo conectar con la API.";
            return View(new List<StockAlertDto>());
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Atender(int id)
    {
        var token = HttpContext.Session.GetString("Token");
        if (token is null)
            return RedirectToAction("Login", "Auth");

        try
        {
            await _api.PostNoContentAsync($"api/inventario/alertas/{id}/atender", token);
            TempData["Ok"] = "Alerta marcada como atendida.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

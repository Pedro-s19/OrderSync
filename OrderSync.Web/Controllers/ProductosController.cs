using Microsoft.AspNetCore.Mvc;
using OrderSync.Web.Services;

namespace OrderSync.Web.Controllers;

public class ProductosController : Controller
{
    private readonly OrderSyncApiClient _api;

    public ProductosController(OrderSyncApiClient api)
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
            var products = await _api.GetAsync<List<ProductDto>>(
                "api/productos",
                token
            ) ?? new List<ProductDto>();

            return View(products);
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return View(new List<ProductDto>());
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "No se pudo conectar con la API.";
            return View(new List<ProductDto>());
        }
    }
}

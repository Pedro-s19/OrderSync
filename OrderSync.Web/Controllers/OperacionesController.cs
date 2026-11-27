using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OrderSync.Web.Security;
using OrderSync.Web.Services;

namespace OrderSync.Web.Controllers;

public sealed class OperacionesController : Controller
{
    private readonly OrderSyncApiClient _api;

    public OperacionesController(OrderSyncApiClient api)
    {
        _api = api;
    }

    private string? Token => HttpContext.Session.GetString("Token");
    private string Role => HttpContext.Session.GetString("Rol") ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> Recetas(int? productoId)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor", "Gerencia"))
            return Forbid();

        ViewBag.Products = await _api.GetAsync<List<ProductDto>>(
            "api/productos?incluirInactivos=true",
            Token
        ) ?? [];
        ViewBag.ProductoId = productoId;

        if (productoId.HasValue)
        {
            try
            {
                ViewBag.Recipe = await _api.GetAsync<JsonElement>(
                    $"api/productos/{productoId}/receta",
                    Token
                );
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Message;
            }
        }

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarReceta(int productoId, string lineasJson)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.IsAdministrator(Role))
            return Forbid();

        try
        {
            using var document = JsonDocument.Parse(lineasJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ApiException("Las líneas deben ser un arreglo JSON.", 400);

            var body = JsonSerializer.SerializeToElement(new
            {
                lineas = document.RootElement
            });

            await _api.PutAsync<JsonElement, JsonElement>(
                $"api/productos/{productoId}/receta",
                body,
                Token
            );

            TempData["Ok"] = "Receta actualizada. Se aplicará al confirmar nuevos pedidos.";
        }
        catch (JsonException)
        {
            TempData["Error"] = "El JSON de ingredientes no es válido.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Recetas), new { productoId });
    }

    [HttpGet]
    public async Task<IActionResult> Compras()
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.Allows(Role, "Supervisor", "Gerencia"))
            return Forbid();

        try
        {
            var orders = await _api.GetAsync<List<JsonElement>>(
                "api/ordenes-compra?pagina=1&tamano=100",
                Token
            ) ?? [];

            return View(orders);
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return View(new List<JsonElement>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> NuevaCompra()
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.IsAdministrator(Role))
            return Forbid();

        ViewBag.Proveedores = await _api.GetAsync<JsonElement>("api/proveedores", Token);
        ViewBag.Ingredientes = await _api.GetAsync<JsonElement>(
            "api/inventario/ingredientes",
            Token
        );

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevaCompra(
        int proveedorId,
        string? fechaEsperada,
        string lineasJson)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.IsAdministrator(Role))
            return Forbid();

        try
        {
            using var document = JsonDocument.Parse(lineasJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ApiException("Las líneas deben ser un arreglo JSON.", 400);

            var body = JsonSerializer.SerializeToElement(new
            {
                proveedorId,
                fechaEsperada = string.IsNullOrWhiteSpace(fechaEsperada)
                    ? null
                    : fechaEsperada,
                lineas = document.RootElement
            });

            await _api.PostAsync<JsonElement, JsonElement>(
                "api/ordenes-compra",
                body,
                Token
            );

            TempData["Ok"] = "Orden de compra creada en borrador.";
            return RedirectToAction(nameof(Compras));
        }
        catch (JsonException)
        {
            TempData["Error"] = "El JSON de líneas no es válido.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        ViewBag.Proveedores = await _api.GetAsync<JsonElement>("api/proveedores", Token);
        ViewBag.Ingredientes = await _api.GetAsync<JsonElement>(
            "api/inventario/ingredientes",
            Token
        );

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AccionCompra(int id, string accion)
    {
        if (Token is null)
            return RedirectToAction("Login", "Auth");

        if (!RoleAccess.IsAdministrator(Role))
            return Forbid();

        try
        {
            if (accion == "enviar")
            {
                await _api.PostNoContentAsync($"api/ordenes-compra/{id}/enviar", Token);
            }
            else if (accion == "cancelar")
            {
                await _api.PostNoContentAsync($"api/ordenes-compra/{id}/cancelar", Token);
            }
            else
            {
                TempData["Error"] = "La acción solicitada no es válida.";
                return RedirectToAction(nameof(Compras));
            }

            TempData["Ok"] = "Orden de compra actualizada.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Compras));
    }
}

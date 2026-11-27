using Microsoft.AspNetCore.Mvc;

namespace OrderSync.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (HttpContext.Session.GetString("Token") is null)
            return RedirectToAction("Login", "Auth");

        ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
        ViewBag.Rol = HttpContext.Session.GetString("Rol");

        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}


using Microsoft.AspNetCore.Mvc;
using OrderSync.Web.Services;

namespace OrderSync.Web.Controllers;

public class AuthController : Controller
{
    private readonly OrderSyncApiClient _api;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        OrderSyncApiClient api,
        ILogger<AuthController> logger)
    {
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetString("Token") is not null)
            return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string email,
        string password,
        string rol)
    {
        ViewBag.Email = email;
        ViewBag.Rol = rol;

        if (string.IsNullOrWhiteSpace(email))
            ModelState.AddModelError("", "Ingresa tu correo electrónico.");

        if (string.IsNullOrWhiteSpace(password))
            ModelState.AddModelError("", "Ingresa tu contraseña.");

        if (string.IsNullOrWhiteSpace(rol))
            ModelState.AddModelError("", "Selecciona tu rol.");

        if (!ModelState.IsValid)
            return View();

        try
        {
            var result = await _api.LoginAsync(
                new LoginDto(email.Trim(), password, rol));

            if (result is null || string.IsNullOrWhiteSpace(result.Token))
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible iniciar sesión. Inténtalo nuevamente.");

                return View();
            }

            HttpContext.Session.Clear();

            HttpContext.Session.SetString("Token", result.Token);
            HttpContext.Session.SetString("Nombre", result.Nombre);
            HttpContext.Session.SetString("Rol", result.Rol);
            HttpContext.Session.SetInt32("UsuarioId", result.UsuarioId);

            return RedirectToAction("Index", "Home");
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View();
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(
                "",
                "No fue posible conectar con OrderSync. Verifica que la API esté ejecutándose.");

            return View();
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(
                "",
                "La solicitud tardó demasiado. Inténtalo nuevamente.");

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en el inicio de sesión.");

            ModelState.AddModelError(
                "",
                "Ocurrió un error inesperado. Inténtalo nuevamente.");

            return View();
        }
    }

    // Mostrar formulario para solicitar el código.
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    // Solicitar código de recuperación.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        ViewBag.Email = email;

        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(
                "",
                "Ingresa tu correo electrónico.");

            return View();
        }

        try
        {
            await _api.RequestPasswordRecoveryAsync(email.Trim());

            TempData["RecoveryMessage"] =
                "Si el correo está registrado, recibirás un código de recuperación. Revisa también tu carpeta de spam.";

            return RedirectToAction(
                nameof(ResetPassword),
                new { email = email.Trim() });
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View();
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(
                "",
                "No fue posible conectar con OrderSync. Verifica que la API esté ejecutándose.");

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al solicitar recuperación de contraseña.");

            ModelState.AddModelError(
                "",
                "No fue posible procesar la solicitud. Inténtalo nuevamente.");

            return View();
        }
    }

    // Mostrar formulario para introducir el código.
    [HttpGet]
    public IActionResult ResetPassword(string? email = null)
    {
        ViewBag.Email = email;

        // El mensaje de correo enviado NO significa que la contraseña cambió.
        ViewBag.RecoveryMessage = TempData["RecoveryMessage"];

        // Solo el POST podrá establecer este mensaje de éxito.
        ViewBag.SuccessMessage = null;

        return View();
    }

    // Validar código y guardar la nueva contraseña.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        string email,
        string code,
        string newPassword,
        string confirmPassword)
    {
        ViewBag.Email = email;
        ViewBag.Code = code;

        if (string.IsNullOrWhiteSpace(email))
            ModelState.AddModelError(
                "",
                "Ingresa tu correo electrónico.");

        if (string.IsNullOrWhiteSpace(code))
            ModelState.AddModelError(
                "",
                "Ingresa el código de recuperación.");
        else if (code.Trim().Length != 6 ||
                 !code.Trim().All(char.IsDigit))
            ModelState.AddModelError(
                "",
                "El código debe contener exactamente 6 números.");

        if (string.IsNullOrWhiteSpace(newPassword))
            ModelState.AddModelError(
                "",
                "Ingresa tu nueva contraseña.");

        if (string.IsNullOrWhiteSpace(confirmPassword))
            ModelState.AddModelError(
                "",
                "Confirma tu nueva contraseña.");
        else if (newPassword != confirmPassword)
            ModelState.AddModelError(
                "",
                "Las contraseñas no coinciden.");

        if (!ModelState.IsValid)
            return View();

        try
        {
            await _api.ResetPasswordAsync(
                email.Trim(),
                code.Trim(),
                newPassword);

            ViewBag.SuccessMessage =
                "¡Tu contraseña se actualizó correctamente! Ya puedes iniciar sesión.";

            ViewBag.Email = email;

            return View();
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View();
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(
                "",
                "No fue posible conectar con OrderSync. Verifica que la API esté ejecutándose.");

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al restablecer la contraseña.");

            ModelState.AddModelError(
                "",
                "No fue posible actualizar la contraseña. Inténtalo nuevamente.");

            return View();
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        return RedirectToAction(nameof(Login));
    }
}

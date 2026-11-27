
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthService auth,
        ILogger<AuthController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    /// <summary>
    /// Inicia sesión y devuelve un token de autenticación.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResult>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        try
        {
            // Ejecuta la autenticación.
            var result = await _auth.LoginAsync(request, ct);

            return Ok(result);
        }
        catch (SecurityValidationException ex)
        {
            // Errores de validación, como seleccionar un rol incorrecto.
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidCredentialsException)
        {
            // No revelamos si el correo existe o si la cuenta está activa.
            return Unauthorized(new
            {
                message = "Correo o contraseña incorrectos."
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // No ocultamos las cancelaciones legítimas de la solicitud.
            throw;
        }
        catch (Exception ex)
        {
            // Los detalles técnicos quedan registrados en el servidor.
            _logger.LogError(ex, "Error inesperado durante el inicio de sesión.");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "No fue posible iniciar sesión. Inténtalo nuevamente."
            });
        }
    }
}

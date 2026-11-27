
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class PasswordRecoveryController : ControllerBase
{
    private readonly PasswordRecoveryService _recoveryService;

    public PasswordRecoveryController(
        PasswordRecoveryService recoveryService)
    {
        _recoveryService = recoveryService;
    }

    [HttpPost("request")]
    public async Task<IActionResult> RequestCode(
        [FromBody] RequestRecoveryCodeRequest request,
        CancellationToken ct)
    {
        await _recoveryService.RequestCodeAsync(
            request.Email,
            ct);

        return Ok(new
        {
            message = "Si el correo está registrado, recibirás un código de recuperación."
        });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        await _recoveryService.ResetPasswordAsync(
            request.Email,
            request.Code,
            request.NewPassword,
            ct);

        return Ok(new
        {
            message = "Tu contraseña se ha actualizado correctamente."
        });
    }
}

public sealed record RequestRecoveryCodeRequest(string Email);

public sealed record ResetPasswordRequest(
    string Email,
    string Code,
    string NewPassword);

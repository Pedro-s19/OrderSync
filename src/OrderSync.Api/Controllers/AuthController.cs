using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderSync.Application.Seguridad;

namespace OrderSync.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    /// <summary>Inicia sesión. La contraseña tiene 8+ caracteres y el rol debe coincidir con el de la cuenta.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await _auth.LoginAsync(request, ct));
}

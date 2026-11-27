using System.Globalization;
using System.Security.Claims;

namespace OrderSync.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id del usuario autenticado (claim "sub" del JWT), o null si no hay.</summary>
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var value = user.FindFirst("sub")?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    public static int RequireUserId(this ClaimsPrincipal user)
        => user.GetUserId() ?? throw new InvalidOperationException("El token no trae el identificador del usuario.");
}

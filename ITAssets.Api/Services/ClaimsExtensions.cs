using System.Security.Claims;
using ITAssets.Api.Middleware;

namespace ITAssets.Api.Services;

public static class ClaimsExtensions
{
    /// <summary>Id del usuario autenticado (claim 'sub'). Se usa para auditar quién ejecutó cada acción.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("sub")?.Value;
        if (!int.TryParse(value, out var id))
            throw new AppException(401, "INVALID_TOKEN", "Token inválido.");
        return id;
    }
}

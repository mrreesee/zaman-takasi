using System.Security.Claims;
using ZamanTakasi.Core.Exceptions;

namespace ZamanTakasi.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>JWT 'sub' claim'inden kullanıcı Id'sini okur (MapInboundClaims=false olduğu için "sub").</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new ForbiddenActionException("Geçerli kullanıcı kimliği bulunamadı.");
    }
}

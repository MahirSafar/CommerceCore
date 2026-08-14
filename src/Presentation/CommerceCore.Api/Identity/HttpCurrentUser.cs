using CommerceCore.Application.Common.Abstractions;
using System.Security.Claims;

namespace CommerceCore.Api.Identity;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated is not true)
                return null;

            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        }
    }
}

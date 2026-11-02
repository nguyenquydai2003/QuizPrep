using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace QuizPrep.Api.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id người dùng từ claim "sub" của JWT (endpoint đã [Authorize] nên luôn có).</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Thiếu thông tin người dùng trong token.");
}

using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public int ExpiresMinutes { get; set; } = 720;

    public SymmetricSecurityKey GetSecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_options.GetSecurityKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [AppClaims.Name] = user.UserName,
                [AppClaims.Role] = user.Role,
            },
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}

public static class AppClaims
{
    public const string Name = "name";
    public const string Role = "role";
}

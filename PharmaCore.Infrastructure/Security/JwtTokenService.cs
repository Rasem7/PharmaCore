using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PharmaCore.Application.Abstractions;

namespace PharmaCore.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "PharmaCore";
    public string Audience { get; set; } = "PharmaCore.Clients";
    /// <summary>Must come from user-secrets or environment (Jwt__SigningKey). At least 32 characters.</summary>
    public string SigningKey { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}

public sealed class JwtTokenService(JwtOptions options) : ITokenService
{
    public string CreateToken(Guid userId, Guid tenantId, string userName, IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new("tenant_id", tenantId.ToString())
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(options.ExpiryMinutes), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

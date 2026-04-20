using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DevInsight.Application.Common;
using DevInsight.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
namespace DevInsight.Infrastructure.Security;
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;
    public string GenerateToken(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured.")));
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email, user.Email), new Claim("org_id", user.OrganizationId.ToString()) };
        var token = new JwtSecurityToken(issuer: _config["Jwt:Issuer"] ?? "DevInsight", audience: _config["Jwt:Audience"] ?? "DevInsight", claims: claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

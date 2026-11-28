using backend.Data.Entities;
using backend.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace backend.Services
{
    public class JwtTokenService(IConfiguration config) : ITokenService
    {
        public string GenerateToken(AppUser user, string verificationStatus)
        {
            var jwtKey = config["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");

            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("verification_status", verificationStatus)
        };

            if (!string.IsNullOrWhiteSpace(user.Email))
                claims.Add(new(ClaimTypes.Email, user.Email));

            if (user.BuyerBusinessId.HasValue)
                claims.Add(new("buyer_business_id", user.BuyerBusinessId.Value.ToString()));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"],
                audience: config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

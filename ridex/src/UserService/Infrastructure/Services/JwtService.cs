using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Services;

public interface IJwtService
{
    (string token, DateTime expiry) GenerateToken(User user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool   Verify(string password, string hash);
}

public class JwtService(IConfiguration cfg) : IJwtService
{
    public (string token, DateTime expiry) GenerateToken(User user)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(cfg["Jwt:ExpiryMinutes"]!));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role,               user.Role),
            new Claim("firstName",                   user.FirstName),
        };

        var token = new JwtSecurityToken(
            issuer:             cfg["Jwt:Issuer"],
            audience:           cfg["Jwt:Audience"],
            claims:             claims,
            expires:            expiry,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }
}

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)   => BCrypt.Net.BCrypt.HashPassword(password, 12);
    public bool   Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

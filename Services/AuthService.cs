using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyBackend.Data;
using MyBackend.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyBackend.Services
{
  public interface IAuthService
  {
    Task<string> AuthenticateUserAsync(string email, string password);
  }

  public class AuthService : IAuthService
  {
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration; // **Field declaration - MUST be present**

    public AuthService(ApplicationDbContext dbContext, IConfiguration configuration) // **Constructor - MUST have IConfiguration parameter**
    {
      _dbContext = dbContext;
      _configuration = configuration; // **Assignment - MUST assign injected configuration to the field**
    }

    public async Task<string> AuthenticateUserAsync(string email, string password)
    {
      var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
      if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
      {
        throw new Exception("Credenciales inválidas");
      }

      return GenerateJWT(user);
    }

    private string GenerateJWT(User user)
    {
      string jwtSecret = _configuration["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET environment variable is not set in AuthService!");
      Console.WriteLine($"JWT_SECRET in GenerateJWT (AuthService.cs) (from direct config): {jwtSecret}"); // Verification log
      if (string.IsNullOrEmpty(jwtSecret)) // Null check
      {
        throw new InvalidOperationException("JWT_SECRET environment variable is not set in AuthService (direct config)!");
      }
      Console.WriteLine($"JWT_SECRET in GenerateJWT (AuthService.cs): {jwtSecret}"); // ADD THIS LINE
      var issuer = _configuration["Jwt:Issuer"];
      var audience = _configuration["Jwt:Audience"];
      var expiryMinutes = Convert.ToDouble(_configuration["Jwt:ExpiryMinutes"]);

      var claims = new[]
      {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim("role", user.Role),
        new Claim("registrationId", user.RegistrationId ?? "")
    };

      var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
      var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
      var token = new JwtSecurityToken(
          issuer,
          audience,
          claims,
          expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
          signingCredentials: creds);

      return new JwtSecurityTokenHandler().WriteToken(token);
    }
  }
}

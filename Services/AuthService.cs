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
using System.Collections.Generic; // Import for HashSet

namespace MyBackend.Services
{
    public interface IAuthService
    {
        Task<string> AuthenticateUserAsync(string email, string password);
        void InvalidateToken(string jti); // Add method to invalidate token
        bool IsTokenInvalidated(string jti); // Add method to check if token is invalidated
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly HashSet<string> _invalidatedTokens = new HashSet<string>(); // In-memory blacklist

        public AuthService(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
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

      var jti = Guid.NewGuid().ToString(); // Generate unique JTI
      var claims = new[]
      {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim("role", user.Role),
        new Claim("registrationId", user.RegistrationId ?? ""),
        new Claim(JwtRegisteredClaimNames.Jti, jti) // Add JTI claim
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

        public void InvalidateToken(string jti)
        {
          Console.WriteLine($"AuthService.InvalidateToken: Invalidating token with JTI: {jti}"); // ADD THIS LINE
            _invalidatedTokens.Add(jti);
        }

        public bool IsTokenInvalidated(string jti)
        {
          Console.WriteLine($"AuthService.InvalidateToken: Token  IS INVALIDATED with JTI: {jti}"); // ADD THIS LINE
            return _invalidatedTokens.Contains(jti);
        }
    }
}

// src/MyBackend/Controllers/AuthController.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyBackend.Services;
using Microsoft.AspNetCore.Authorization;
using System.Linq; // Add this for FirstOrDefault
using Microsoft.Extensions.Configuration;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
  private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

  public AuthController(IAuthService authService, IConfiguration configuration) // Update the constructor
  {
    _authService = authService;
    _configuration = configuration; // Assign injected configuration
  }

  [HttpPost("login")]
  public async Task<IActionResult> Login([FromBody] LoginRequest model)
  {
    // ...
    try
    {
      // Log the JWT Secret being used
      Console.WriteLine($"AuthController.Login: JWT_SECRET = {_configuration["JWT_SECRET"]}");
      var token = await _authService.AuthenticateUserAsync(model.Email, model.Password);

      // Log cookie settings
      Console.WriteLine("AuthController.Login: Setting cookie");
      Response.Cookies.Append("token", token, new CookieOptions
      {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Expires = DateTime.UtcNow.AddHours(1),
        Domain = "dotnet-embed-tokent-powerbi.onrender.com",
        Path = "/"
      });

      return Ok(new { message = "Inicio de sesión exitoso" });
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error en Login: {ex}");
      return Unauthorized(new { error = "Credenciales inválidas" });
    }
  }

  [HttpPost("logout")]
  public IActionResult Logout()
  {
    Console.WriteLine("AuthController.Logout: Deleting cookie");
    Response.Cookies.Delete("token", new CookieOptions()
    {
      Domain = "dotnet-embed-tokent-powerbi.onrender.com", //replace with your domain
      Path = "/" // Applies to all routes
    });
    return Ok(new { message = "Sesión cerrada" });
  }

  [Authorize]
  [HttpGet("check")]
  [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)] //Recommended
  public IActionResult Check()
  {
    // Si se llega aquí, la autenticación ha sido exitosa gracias a la validación JWT

    // Extract email and role from claims
    var emailClaim = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
    var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "role");

    string userEmail = emailClaim?.Value ?? "";
    string userRole = roleClaim?.Value ?? "";

    return Ok(new
    {
      isAuthenticated = true,
      user = User.Identity?.Name ?? "", // User.Identity?.Name will now likely be the email as we set it in GenerateJWT
      email = userEmail,
      role = userRole
    });
  }

  public class LoginRequest
  {
    public required string Email { get; set; } // Add required
    public required string Password { get; set; } // Add required
  }
}
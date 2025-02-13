using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyBackend.Services;

namespace MyBackend.Controllers
{
  [ApiController]
  [Route("[controller]")]
  public class AuthController : ControllerBase
  {
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
      _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest model)
    {
      if (!ModelState.IsValid) // Validación del modelo
      {
        return BadRequest(ModelState); // Devuelve errores de validación
      }

      try
      {
        var token = await _authService.AuthenticateUserAsync(model.Email, model.Password);

        // Configurar la cookie (ajusta las opciones según sea necesario)
        Response.Cookies.Append("token", token, new Microsoft.AspNetCore.Http.CookieOptions
        {
          HttpOnly = true,
          Secure = true,
          SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None,
          Expires = DateTime.UtcNow.AddHours(1),
          Path = "/"
        });

        return Ok(new { message = "Inicio de sesión exitoso" });
      }
      catch (Exception ex)
      {
        // Log del error detallado (usando un logger adecuado, como ILogger<AuthController>)
        Console.WriteLine($"Error en Login: {ex}"); // Reemplaza con logging adecuado

        return Unauthorized(new { error = "Credenciales inválidas" }); // Mensaje genérico
      }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
      Response.Cookies.Delete("token");
      return Ok(new { message = "Sesión cerrada" });
    }

    [HttpGet("check")]
    public IActionResult Check()
    {
      // Se asume que la autenticación via JWT ya se realizó
      return Ok(new { isAuthenticated = true, user = User.Identity?.Name ?? "" });
    }
  }

  public class LoginRequest
  {
    public required string Email { get; set; } // Add required
    public required string Password { get; set; } // Add required
  }
}

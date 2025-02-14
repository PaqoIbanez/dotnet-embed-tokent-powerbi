// src/MyBackend/Controllers/AuthController.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyBackend.Services;
using Microsoft.AspNetCore.Authorization;
using System.Linq; // Add this for FirstOrDefault

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
        // ... (your existing Login action code) ...
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var token = await _authService.AuthenticateUserAsync(model.Email, model.Password);

            // Configurar la cookie (ajusta las opciones según sea necesario)
            Response.Cookies.Append("token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddHours(1),
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
        Response.Cookies.Delete("token");
        return Ok(new { message = "Sesión cerrada" });
    }

    [Authorize]
    [HttpGet("check")]
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
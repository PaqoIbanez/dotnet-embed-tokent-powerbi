using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyBackend.Services;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http; // Para CookieOptions, SameSiteMode, etc.

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest model)
    {
        try
        {
            // 1. Autenticar usuario
            var token = await _authService.AuthenticateUserAsync(model.Email, model.Password);

            // 2. Tomar el dominio desde configuración (o usar uno fijo si lo prefieres)
            //    Ajusta "Cookies:Domain" en tu appsettings.json o en tus variables de entorno.
            //    Si no está definido, asumimos "dotnet-embed-tokent-powerbi.onrender.com" (TU caso).
            var cookieDomain = _configuration["Cookies:Domain"] 
                               ?? "dotnet-embed-tokent-powerbi.onrender.com";

            Console.WriteLine("AuthController.Login: Setting cookie with domain: " + cookieDomain);

            // 3. Crear la cookie con los atributos para third-party
            Response.Cookies.Append("token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,  // ← imprescindible para cross-site
                Domain = cookieDomain,         // ← debe coincidir después al borrarla
                Path = "/",                    // por lo general se usa "/"
                Expires = DateTime.UtcNow.AddHours(1)
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
        Console.WriteLine("AuthController.Logout: Deleting cookie 'token'");

        // IMPORTANTE: utilizar los mismos atributos 
        // (nombre, domain, path, SameSite, Secure, etc.) 
        var cookieDomain = _configuration["Cookies:Domain"] 
                           ?? "dotnet-embed-tokent-powerbi.onrender.com";

        Response.Cookies.Delete(
            "token",
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None, 
                Domain = cookieDomain,
                Path = "/"
                // No hace falta Expire, internamente .NET genera la fecha 1970
            }
        );

        return Ok(new { message = "Sesión cerrada" });
    }

    [Authorize]
    [HttpGet("check")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Check()
    {
        // Si llega aquí, la autenticación via JWT ha sido exitosa
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
        var roleClaim  = User.Claims.FirstOrDefault(c => c.Type == "role");

        return Ok(new
        {
            isAuthenticated = true,
            user  = User.Identity?.Name ?? "",
            email = emailClaim?.Value ?? "",
            role  = roleClaim?.Value ?? ""
        });
    }

    public class LoginRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}

// src/MyBackend/Controllers/AuthController.cs
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

  // <-- Agregamos [Authorize] para que sólo se pueda acceder si hay un token válido
  [Authorize]
  [HttpGet("check")]
  public IActionResult Check()
  {
    // Si se llega aquí, la autenticación ha sido exitosa gracias a la validación JWT
    return Ok(new { isAuthenticated = true, user = User.Identity?.Name ?? "" });
  }

    public class LoginRequest
  {
    public required string Email { get; set; } // Add required
    public required string Password { get; set; } // Add required
  }
}

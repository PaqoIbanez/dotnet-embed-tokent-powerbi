using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyBackend.Services;
using System.Linq;
using System.Threading.Tasks;

namespace MyBackend.Controllers
{
  [ApiController]
  [Route("[controller]")]
  [Authorize] // Make sure this is still here and the namespace is imported
  public class EmbedController : ControllerBase
  {
    private readonly IPowerBiService _powerBiService;
    private readonly IAuthorizationService _authorizationService; // Inject IAuthorizationService

    public EmbedController(IPowerBiService powerBiService, IAuthorizationService authorizationService)
    {
      _powerBiService = powerBiService;
      _authorizationService = authorizationService;
    }

    [HttpGet("getEmbedToken")]
    // [Authorize(Policy = "CanViewPowerBIReport")]
    public async Task<IActionResult> GetEmbedToken()
    {
      await Task.Yield(); // Explicitly make the method asynchronous, suppress CS1998 for now.
      try
      {
        var email = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
        var role = User.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(role))
          return Unauthorized(new { error = "Token inválido o expirado" });

        var user = new AuthenticatedUser
        {
          Email = email,
          Role = role
        };

        bool isAuthorizedToAccessReport = await IsUserAuthorizedForReport(user);

        if (!isAuthorizedToAccessReport)
        {
          return Forbid("No autorizado para acceder a este informe.");
        }

        var embedInfo = await _powerBiService.GetEmbedInfoAsync(user);
        return Ok(new
        {
          accessToken = embedInfo.AccessToken,
          embedUrl = embedInfo.EmbedUrl,
          expiry = embedInfo.Expiry,
          datasetId = embedInfo.DatasetId
        });
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { error = "Error interno del servidor", detalle = ex.Message });
      }
    }

    private Task<bool> IsUserAuthorizedForReport(AuthenticatedUser user)
    {
      // **Implement your actual authorization logic here.**
      // This is a placeholder.
      // Example: Check a database table, business rules, etc., based on user and report.
      // For instance, you might check if the user's role or user ID is associated with the report in some way.

      // For now, just a placeholder to always return true (replace with real logic!)
      return Task.FromResult(true); // **Replace this with your authorization logic!**
    }
  }
}
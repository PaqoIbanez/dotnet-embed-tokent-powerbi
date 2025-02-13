using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

namespace MyBackend.Services
{
  public class PowerBiEmbedInfo
  {
    public required string AccessToken { get; set; } // Add required
    public required string EmbedUrl { get; set; }   // Add required
    public DateTime Expiry { get; set; }
    public required string DatasetId { get; set; }  // Add required
  }

  // Clase para representar al usuario autenticado (lo que recibes en el JWT)
  public class AuthenticatedUser
  {
    public required string Email { get; set; } // Add required
    public required string Role { get; set; }  // Add required
  }

  public interface IPowerBiService
  {
    Task<PowerBiEmbedInfo> GetEmbedInfoAsync(AuthenticatedUser user);
  }

  public class PowerBiService : IPowerBiService
  {
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfidentialClientApplication _msalClient;

    public PowerBiService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
      _configuration = configuration;
      _httpClientFactory = httpClientFactory;

      string authorityUrl = configuration.GetValue<string>("Azure:AuthorityUrl") ?? throw new InvalidOperationException("Configuration 'Azure:AuthorityUrl' is missing."); // Ensure it's not null
      string tenantId = configuration.GetValue<string>("PowerBI:TenantId") ?? throw new InvalidOperationException("Configuration 'PowerBI:TenantId' is missing."); // Ensure it's not null

      Console.WriteLine($"Azure:AuthorityUrl from config: {authorityUrl}"); // Log AuthorityUrl
      Console.WriteLine($"PowerBI:TenantId from config: {tenantId}");     // Log TenantId

      string authorityUriString = $"{authorityUrl}{tenantId}";
      Console.WriteLine($"Constructed Authority URI String: {authorityUriString}"); // Log constructed URI string

      try
      {
        _msalClient = ConfidentialClientApplicationBuilder.Create(_configuration["PowerBI:ClientId"])
       .WithClientSecret(_configuration["PowerBI:ClientSecret"])
       .WithAuthority(new Uri($"{authorityUrl}{tenantId}"))
       .Build();
      }
      catch (UriFormatException ex)
      {
        Console.WriteLine($"URI Format Exception: {ex.Message}"); // Log specific URI exception details
        throw; // Re-throw the exception to see the error in the startup logs
      }
    }

    public async Task<PowerBiEmbedInfo> GetEmbedInfoAsync(AuthenticatedUser user)
    {
      // 1. Obtener token de acceso para llamar a la API de Power BI
      var scopes = new[] { _configuration["Azure:Scope"] };
      var authResult = await _msalClient.AcquireTokenForClient(scopes).ExecuteAsync();
      var accessToken = authResult.AccessToken;

      // 2. Obtener detalles del reporte
      var workspaceId = _configuration["PowerBI:WorkspaceId"];
      var reportId = _configuration["PowerBI:ReportId"];
      var baseUrl = "https://api.powerbi.com/";
      var reportDetailsUrl = $"{baseUrl}v1.0/myorg/groups/{workspaceId}/reports/{reportId}";

      var client = _httpClientFactory.CreateClient();
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
      var reportResponse = await client.GetAsync(reportDetailsUrl);
      if (!reportResponse.IsSuccessStatusCode)
        throw new Exception("Error al obtener detalles del reporte desde Power BI");

      using var reportStream = await reportResponse.Content.ReadAsStreamAsync();
      var reportData = await JsonSerializer.DeserializeAsync<JsonElement>(reportStream);
      var embedUrl = reportData.GetProperty("embedUrl").GetString();
      var datasetId = reportData.GetProperty("datasetId").GetString();

      if (string.IsNullOrEmpty(embedUrl))
        throw new Exception("Embed URL no disponible en la respuesta de Power BI");
      if (string.IsNullOrEmpty(datasetId))
        throw new Exception("DatasetId no disponible en la respuesta de Power BI");

      // 3. Generar embed token
      var roleForRLS = user.Role == "teacher" ? "FiltroMentor" : "FiltroAlumno";

      var generateTokenUrl = $"{baseUrl}v1.0/myorg/GenerateToken";

      var bodyObj = new
      {
        reports = new[] { new { id = reportId, groupId = workspaceId } },
        datasets = new[] { new { id = datasetId } },
        identities = new[]
          {
        new {
          username = user.Email,
          roles = new[] { roleForRLS },
          datasets = new[] { datasetId }
        }
      }
      };

      var bodyJson = JsonSerializer.Serialize(bodyObj);
      var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

      var tokenResponse = await client.PostAsync(generateTokenUrl, content);
      if (!tokenResponse.IsSuccessStatusCode)
        throw new Exception("Error al generar el token de incrustación");

      using var tokenStream = await tokenResponse.Content.ReadAsStreamAsync();
      var tokenData = await JsonSerializer.DeserializeAsync<JsonElement>(tokenStream);
      var embedToken = tokenData.GetProperty("token").GetString() ?? throw new Exception("Embed token is null"); // Line 109
      var expiration = tokenData.GetProperty("expiration").GetDateTime();


      return new PowerBiEmbedInfo
      {
        AccessToken = embedToken,
        EmbedUrl = embedUrl,
        Expiry = expiration,
        DatasetId = datasetId // Asigna el datasetId aquí
      };
    }

  }
}

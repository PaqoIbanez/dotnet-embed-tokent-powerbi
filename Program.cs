using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyBackend.Data;
using MyBackend.Middleware;
using MyBackend.Services;
using System.Text;
using dotenv.net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpsPolicy;

// Cargar las variables del archivo .env
DotEnv.Load();

// Desactivar el mapeo automático de claims (opcional, según tus necesidades)
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor
builder.Services.AddControllers();

// Configurar la conexión a PostgreSQL usando la variable DefaultConnection del .env
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration["DefaultConnection"] ?? 
        throw new InvalidOperationException("La variable de entorno DefaultConnection no está definida!")
    )
);

// Registrar servicios de la aplicación
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPowerBiService, PowerBiService>();
builder.Services.AddHttpClient();

// Configurar rate limiting (limitar solicitudes)
builder.Services.AddRateLimiter(options =>
{
  options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
      RateLimitPartition.GetFixedWindowLimiter(
          partitionKey: httpContext.Request.Headers.Host.ToString() ?? 
                        httpContext.Request.HttpContext.Connection.RemoteIpAddress?.ToString()!,
          factory: partition => new FixedWindowRateLimiterOptions
          {
            PermitLimit = 15, // Máximo 15 solicitudes
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5,
          }
      )
  );
  options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Configurar la autenticación JWT
string jwtSecret = builder.Configuration["JWT_SECRET"] ??
                   throw new InvalidOperationException("La variable JWT_SECRET no está definida!");
Console.WriteLine($"JWT_SECRET desde el entorno (Program.cs): {jwtSecret}");

var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
  options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
  options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
  options.Events = new JwtBearerEvents
  {
    OnAuthenticationFailed = context =>
    {
      Console.WriteLine($"Fallo en la autenticación JWT: {context.Exception.Message}");
      return Task.CompletedTask;
    },
    OnTokenValidated = context =>
    {
      Console.WriteLine("Token JWT validado correctamente.");

      // Get the IAuthService instance
      var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();

      // Get the JTI claim
      var jti = context.SecurityToken.Id;
        if (string.IsNullOrEmpty(jti))
        {
            context.Fail("JTI claim is missing.");
            return Task.CompletedTask;
        }

      // Check if the token is invalidated
      if (authService.IsTokenInvalidated(jti))
      {
        context.Fail("This token has been invalidated.");
        return Task.CompletedTask;
      }

      return Task.CompletedTask;
    }
  };
  options.TokenValidationParameters = new TokenValidationParameters
  {
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = issuer,
    ValidAudience = audience,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
  };
});

// Configurar CORS usando la variable AllowedDomains del .env
builder.Services.AddCors(options =>
{
  options.AddPolicy("CorsPolicy", policyBuilder =>
  {
    var allowedDomains = builder.Configuration["AllowedDomains"]?
                           .Split(',') 
                           ?? new string[] { "http://localhost:4200" };
    policyBuilder.WithOrigins(allowedDomains)
                 .AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials();
  });
});

// Agregar política de autorización (ejemplo)
builder.Services.AddAuthorization(options =>
{
  options.AddPolicy("CanViewPowerBIReport", policy =>
        policy.RequireRole("FiltroAlumno", "FiltroMentor"));
});

var app = builder.Build();

// Redirección forzada a HTTPS
app.UseHttpsRedirection();

string cspValue = "default-src 'self';" +
                  "script-src 'self' https://cdn.example.com;" +
                  "style-src 'self' https://fonts.example.com;" +
                  "img-src 'self' data:;" +
                  "font-src 'self' https://fonts.gstatic.com;" +
                  "connect-src 'self' https://api.example.com;";
app.UseRateLimiter();
app.UseHsts();

// Agregar cabeceras de seguridad
app.Use(async (context, next) =>
{
  context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
  context.Response.Headers.Append("X-Frame-Options", "DENY");
  context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
  context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=()");
  context.Response.Headers.Append("Content-Security-Policy", cspValue);
  await next();
});

app.UseCors("CorsPolicy");

// Middleware para extraer el token de la cookie y agregarlo al header Authorization
app.UseMiddleware<JwtCookieMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

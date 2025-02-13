using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MyBackend.Middleware
{
  public class JwtCookieMiddleware
  {
    private readonly RequestDelegate _next;

    public JwtCookieMiddleware(RequestDelegate next)
    {
      _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
      // Si no se encuentra el header Authorization, intenta leer el token de la cookie "token"
      if (!context.Request.Headers.ContainsKey("Authorization"))
      {
        var token = context.Request.Cookies["token"];
        if (!string.IsNullOrEmpty(token))
        {
          context.Request.Headers["Authorization"] = "Bearer " + token;
        }
      }
      await _next(context);
    }
  }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CasaSim.Api.Auth;

/// <summary>
/// Minimal API key authentication for admin endpoints.
/// Checks the X-Api-Key header against AdminSettings:ApiKey in configuration.
/// </summary>
public sealed class AdminAuthenticationFilter : IAsyncAuthorizationFilter
{
    private readonly string _apiKey;

    public AdminAuthenticationFilter(IConfiguration configuration)
    {
        _apiKey = configuration["AdminSettings:ApiKey"] ?? string.Empty;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            context.Result = new StatusCodeResult(500);
            return Task.CompletedTask;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var providedKey) ||
            providedKey.Count != 1 ||
            providedKey[0] != _apiKey)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "Unauthorized. Provide a valid X-Api-Key header."
            });
        }

        return Task.CompletedTask;
    }
}

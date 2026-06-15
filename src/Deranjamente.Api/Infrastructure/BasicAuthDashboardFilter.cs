using System.Net.Http.Headers;
using System.Text;
using Hangfire.Dashboard;

namespace Deranjamente.Api.Infrastructure;

/// <summary>
/// Protects the Hangfire dashboard with HTTP Basic Auth from env-var credentials
/// (PRD: the dashboard and admin actions are Basic-Auth protected in v1). When no
/// credentials are configured (e.g. local dev) access is allowed.
/// </summary>
public class BasicAuthDashboardFilter(string? username, string? password) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return true; // not configured → open (local dev)
        }

        var httpContext = context.GetHttpContext();
        var header = httpContext.Request.Headers.Authorization.ToString();

        if (AuthenticationHeaderValue.TryParse(header, out var parsed)
            && parsed.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase)
            && parsed.Parameter is { } token)
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var separator = decoded.IndexOf(':');
            if (separator > 0)
            {
                var user = decoded[..separator];
                var pass = decoded[(separator + 1)..];
                if (user == username && pass == password)
                {
                    return true;
                }
            }
        }

        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire\"";
        return false;
    }
}

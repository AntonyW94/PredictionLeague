using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace ThePredictions.API.Controllers;

[ApiController]
public abstract class AuthControllerBase(IConfiguration configuration) : ApiControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    // Shared across the www/dev subdomains in real environments. Local development
    // (localhost / loopback IPs) can't match this domain, so the cookie falls back
    // to a host-only cookie there - see BuildCookieOptions.
    private const string SharedCookieDomain = ".thepredictions.co.uk";

    protected void SetTokenCookie(string token)
    {
        var expiryDays = double.Parse(configuration["JwtSettings:RefreshTokenExpiryDays"]!);

        var cookieOptions = BuildCookieOptions();
        cookieOptions.Expires = DateTime.UtcNow.AddDays(expiryDays);

        try
        {
            Response.Cookies.Append(RefreshTokenCookieName, token, cookieOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to set 'refreshToken' cookie.", ex);
        }
    }

    protected void DeleteTokenCookie()
    {
        // A cookie can only be cleared by a Set-Cookie whose Domain/Path/Secure/SameSite
        // match the original, so reuse the same options the cookie was written with.
        Response.Cookies.Delete(RefreshTokenCookieName, BuildCookieOptions());
    }

    private CookieOptions BuildCookieOptions()
    {
        var isLocal = IsLocalHost(Request.Host.Host);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Path = "/",
            // SameSite=None requires Secure. On localhost we may be served over plain
            // HTTP, and the cookie is first-party (client and API share an origin), so a
            // host-only Lax cookie works without needing HTTPS.
            Secure = isLocal ? Request.IsHttps : true,
            SameSite = isLocal ? SameSiteMode.Lax : SameSiteMode.None
        };

        if (!isLocal)
            cookieOptions.Domain = SharedCookieDomain;

        return cookieOptions;
    }

    private static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || IPAddress.TryParse(host, out _);
}

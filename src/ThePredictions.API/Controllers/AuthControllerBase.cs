using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace ThePredictions.API.Controllers;

[ApiController]
public abstract class AuthControllerBase(IConfiguration configuration) : ApiControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    // Companion flag cookie recording the user's "remember me" choice at login. The server can't
    // otherwise tell whether a refresh-token cookie it receives was persistent or session-scoped, so
    // the refresh endpoint reads this to re-issue the cookie with the same lifetime.
    private const string RememberMeCookieName = "rememberMe";

    // Shared across the www/dev subdomains in real environments. Local development
    // (localhost / loopback IPs) can't match this domain, so the cookie falls back
    // to a host-only cookie there - see BuildCookieOptions.
    private const string SharedCookieDomain = ".thepredictions.co.uk";

    // persistent = true keeps the user signed in across browser restarts (a dated cookie for the
    // configured refresh-token lifetime); false writes a session cookie that the browser clears on
    // close. The companion rememberMe cookie is written with the same lifetime so a later refresh
    // can preserve the choice.
    protected void SetTokenCookie(string token, bool persistent)
    {
        var cookieOptions = BuildCookieOptions();
        if (persistent)
            cookieOptions.Expires = PersistentCookieExpiry();

        try
        {
            Response.Cookies.Append(RefreshTokenCookieName, token, cookieOptions);
            Response.Cookies.Append(RememberMeCookieName, persistent ? "1" : "0", cookieOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to set 'refreshToken' cookie.", ex);
        }
    }

    // Whether a refresh should re-issue a persistent cookie, read from the companion flag written at
    // login. Absent (a session predating remember-me) defaults to persistent, preserving old behaviour.
    protected bool ShouldPersistSession() => Request.Cookies[RememberMeCookieName] != "0";

    // Writes only the remember-me preference cookie (not the refresh-token cookie). The external
    // (Google) sign-in redirect uses this to mark the session persistent before the client exchanges
    // the URL token at the refresh endpoint, so all Google logins are remembered by default.
    protected void SetRememberMePreference(bool persistent)
    {
        var cookieOptions = BuildCookieOptions();
        if (persistent)
            cookieOptions.Expires = PersistentCookieExpiry();

        Response.Cookies.Append(RememberMeCookieName, persistent ? "1" : "0", cookieOptions);
    }

    private DateTimeOffset PersistentCookieExpiry() =>
        DateTime.UtcNow.AddDays(double.Parse(configuration["JwtSettings:RefreshTokenExpiryDays"]!));

    protected void DeleteTokenCookie()
    {
        // A cookie can only be cleared by a Set-Cookie whose Domain/Path/Secure/SameSite
        // match the original, so reuse the same options the cookie was written with.
        var cookieOptions = BuildCookieOptions();
        Response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
        Response.Cookies.Delete(RememberMeCookieName, cookieOptions);
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

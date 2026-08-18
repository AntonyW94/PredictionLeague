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

    // Until August 2026 both cookies were scoped to this domain so www and dev could share them. What
    // that actually meant was dev and prod sharing ONE refresh token: a cookie is identified by name,
    // domain and path, so signing into either site replaced the other's, and the site whose database had
    // never issued that token bounced the player to a login screen. Nothing needed the wider scope - the
    // client and API are same-origin in every environment, and the apex 301s to www - so the cookies are
    // host-only now and each environment keeps its own. Kept only to expire what the old scope left
    // behind; see ExpireLegacyDomainCookies.
    private const string LegacyCookieDomain = ".thepredictions.co.uk";

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
            ExpireLegacyDomainCookies();
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
        ExpireLegacyDomainCookies();
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

        ExpireLegacyDomainCookies();
    }

    /// <summary>
    /// Expires the domain-scoped pair written before these cookies became host-only.
    /// </summary>
    /// <remarks>
    /// A host-only cookie does not replace a domain cookie of the same name: the browser keeps both and
    /// sends both, and the server reads whichever the header happens to list first. Left alone, a player
    /// could go on presenting a stale token belonging to the other environment for the whole refresh-token
    /// lifetime - the exact problem the host-only change exists to end - so every write clears the old pair.
    ///
    /// Deleting after writing is safe: Response.Cookies.Delete drops any pending Set-Cookie of the same
    /// name from the response, but only where the domain and path also match, so clearing the old
    /// domain-scoped pair leaves the host-only cookies just written intact. AuthControllerBaseCookieTests
    /// pins that, because the two are one line apart and a silent strip would sign everybody out.
    ///
    /// Transitional. Safe to remove once the longest refresh-token lifetime has passed since release.
    /// </remarks>
    private void ExpireLegacyDomainCookies()
    {
        // Localhost never wrote a domain cookie, so there is nothing there to clear.
        if (IsLocalHost(Request.Host.Host))
            return;

        var legacyOptions = BuildCookieOptions();
        legacyOptions.Domain = LegacyCookieDomain;

        Response.Cookies.Delete(RefreshTokenCookieName, legacyOptions);
        Response.Cookies.Delete(RememberMeCookieName, legacyOptions);
    }

    private CookieOptions BuildCookieOptions()
    {
        var isLocal = IsLocalHost(Request.Host.Host);

        // No Domain, deliberately: a host-only cookie is what keeps dev and prod from sharing a session.
        return new CookieOptions
        {
            HttpOnly = true,
            Path = "/",
            // SameSite=None requires Secure. On localhost we may be served over plain
            // HTTP, and the cookie is first-party (client and API share an origin), so a
            // Lax cookie works without needing HTTPS.
            Secure = isLocal ? Request.IsHttps : true,
            SameSite = isLocal ? SameSiteMode.Lax : SameSiteMode.None
        };
    }

    private static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || IPAddress.TryParse(host, out _);
}

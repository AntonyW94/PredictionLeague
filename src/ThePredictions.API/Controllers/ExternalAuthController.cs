using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ThePredictions.Application.Features.Authentication.Commands.LoginWithGoogle;
using ThePredictions.Contracts.Authentication;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;

namespace ThePredictions.API.Controllers;

[Route("external-auth")]
[EnableRateLimiting("auth")]
[SwaggerTag("Authentication - OAuth login with Google")]
public class ExternalAuthController(ILogger<ExternalAuthController> logger, IMediator mediator, IConfiguration configuration) : AuthControllerBase(configuration)
{
    [HttpGet("google-login")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Initiate Google OAuth login",
        Description = "Redirects to Google's OAuth consent screen. After authentication, Google redirects back to the callback endpoint which then redirects to the client application with tokens.")]
    [SwaggerResponse(302, "Redirect to Google OAuth")]
    public IActionResult GoogleLogin(
        [FromQuery, SwaggerParameter("URL to redirect to after authentication completes")] string returnUrl,
        [FromQuery, SwaggerParameter("Source page for error redirects")] string source)
    {
        logger.LogInformation("Called google-login");

        // Validate and sanitise redirect URLs to prevent open redirect attacks
        var safeReturnUrl = GetSafeLocalPath(returnUrl, "/");
        var safeSource = GetSafeLocalPath(source, "/login");

        var callbackUrl = Url.Action("GoogleCallback");
        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl,
            Items =
            {
                { "returnUrl", safeReturnUrl },
                { "source", safeSource }
            }
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("signin-google")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Google OAuth callback (internal)",
        Description = "Callback endpoint for Google OAuth. Processes the authentication response, creates/updates user account, generates tokens, and redirects to the client application. Not intended to be called directly.")]
    [SwaggerResponse(302, "Redirect to client application with tokens")]
    [SwaggerResponse(400, "OAuth authentication failed")]
    public async Task<IActionResult> GoogleCallbackAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Called signin-google");

        var authenticateResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);

        // Defence in depth - validate URLs again before redirect
        var returnUrl = SafePathFromProperties(authenticateResult, "returnUrl", "/");
        var source = SafePathFromProperties(authenticateResult, "source", "/login");

        var command = new LoginWithGoogleCommand(authenticateResult, source);
        var result = await mediator.Send(command, cancellationToken);

        switch (result)
        {
            case SuccessfulAuthenticationResponse success:
                // Google sign-ins are always remembered: mark the session persistent so the follow-up
                // refresh-token exchange (the client posts the URL token) writes a persistent cookie,
                // regardless of any remember-me preference left over from a prior email login.
                SetRememberMePreference(persistent: true);
                var encodedToken = WebUtility.UrlEncode(success.RefreshTokenForCookie);
                return Redirect($"{returnUrl}?refreshToken={encodedToken}&source={source}");

            case ExternalLoginFailedAuthenticationResponse failure:
                return RedirectWithError(failure.Source, failure.Message);

            default:
                logger.LogError("Google Login result was ERROR");
                return RedirectWithError(source, "An unknown authentication error occurred.");
        }
    }

    /// <summary>
    /// Reads a redirect target back off the sign-in properties and forces it to a local path. The
    /// value round-tripped through the external provider, so it is treated as untrusted on the way
    /// back and any tampering is logged.
    /// </summary>
    private string SafePathFromProperties(AuthenticateResult authenticateResult, string key, string fallback)
    {
        var value = authenticateResult.Properties?.Items[key] ?? fallback;
        var safeValue = GetSafeLocalPath(value, fallback);

        if (safeValue != value)
            logger.LogWarning("Invalid {Key} detected in callback: {Value}", key, value);

        return safeValue;
    }

    private IActionResult RedirectWithError(string returnUrl, string error)
    {
        var safeReturnUrl = GetSafeLocalPath(returnUrl, "/login");
        return Redirect($"{safeReturnUrl}?error={Uri.EscapeDataString(error)}");
    }

    /// <summary>
    /// Extracts a safe local path from a URL, handling full URLs, relative paths, and bare paths.
    /// Returns the fallback if the URL is invalid or points to an external site.
    /// </summary>
    private string GetSafeLocalPath(string? url, string fallback)
    {
        if (string.IsNullOrEmpty(url))
            return fallback;

        // Handle full URLs - extract path if host matches.
        // The leading-slash check has to come first. On Linux, Uri.TryCreate accepts "/dashboard"
        // as the absolute file URI "file:///dashboard", whose empty host then fails the same-host
        // test below and silently discards a perfectly valid local return URL. Windows rejects the
        // same string, so without this guard the redirect target only breaks once deployed.
        // Paths beginning "//" or containing a backslash still fall through to IsValidLocalPath.
        if (!url.StartsWith('/') && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Check if the URL's host matches our host (normalise to handle www/non-www mismatch)
            var requestHost = NormaliseHost(Request.Host.Host);
            var urlHost = NormaliseHost(uri.Host);
            if (!string.Equals(urlHost, requestHost, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Rejected external redirect URL: {Url}", url);
                return fallback;
            }

            // Extract just the path and query from the URL
            url = uri.PathAndQuery;
        }

        // Handle bare paths like "login" by prepending /
        if (!url.StartsWith('/'))
            url = "/" + url;

        // Validate the path
        return !IsValidLocalPath(url) ? fallback : url;
    }

    /// <summary>
    /// Validates that a path is safe for redirection.
    /// </summary>
    private static bool IsValidLocalPath(string path)
    {
        // Block protocol-relative URLs (//evil.com)
        if (path.StartsWith("//"))
            return false;

        // Block URLs with backslash (/\evil.com in some browsers)
        return !path.Contains('\\');
    }

    /// <summary>
    /// Normalises a host by stripping the www. prefix to handle www/non-www mismatches.
    /// </summary>
    private static string NormaliseHost(string host)
    {
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? host[4..]
            : host;
    }
}
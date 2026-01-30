using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PredictionLeague.Application.Features.Authentication.Commands.LoginWithGoogle;
using PredictionLeague.Contracts.Authentication;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;

namespace PredictionLeague.API.Controllers;

[Route("external-auth")]
[EnableRateLimiting("auth")]
[SwaggerTag("Authentication - OAuth login with Google")]
public class ExternalAuthController : AuthControllerBase
{
    private readonly ILogger<ExternalAuthController> _logger;
    private readonly IMediator _mediator;

    public ExternalAuthController(ILogger<ExternalAuthController> logger, IMediator mediator, IConfiguration configuration) : base(configuration)
    {
        _logger = logger;
        _mediator = mediator;
    }

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
        _logger.LogInformation("Called google-login");

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
        _logger.LogInformation("Called signin-google");

        var authenticateResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        var returnUrl = authenticateResult.Properties?.Items["returnUrl"] ?? "/";
        var source = authenticateResult.Properties?.Items["source"] ?? "/login";

        // Defence in depth - validate URLs again before redirect
        var safeReturnUrl = GetSafeLocalPath(returnUrl, "/");
        var safeSource = GetSafeLocalPath(source, "/login");

        if (safeReturnUrl != returnUrl)
            _logger.LogWarning("Invalid returnUrl detected in callback: {ReturnUrl}", returnUrl);

        if (safeSource != source)
            _logger.LogWarning("Invalid source detected in callback: {Source}", source);

        returnUrl = safeReturnUrl;
        source = safeSource;

        var command = new LoginWithGoogleCommand(authenticateResult, source);
        var result = await _mediator.Send(command, cancellationToken);

        switch (result)
        {
            case SuccessfulAuthenticationResponse success:
                var encodedToken = WebUtility.UrlEncode(success.RefreshTokenForCookie);
                return Redirect($"{returnUrl}?refreshToken={encodedToken}&source={source}");

            case ExternalLoginFailedAuthenticationResponse failure:
                return RedirectWithError(failure.Source, failure.Message);

            default:
                _logger.LogError("Google Login result was ERROR");
                return RedirectWithError(source, "An unknown authentication error occurred.");
        }
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

        // Handle full URLs - extract path if host matches
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Check if the URL's host matches our host
            var requestHost = Request.Host.Host;
            if (!string.Equals(uri.Host, requestHost, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Rejected external redirect URL: {Url}", url);
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
}
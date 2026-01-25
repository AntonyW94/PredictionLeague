using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Authentication.Commands.LoginWithGoogle;
using PredictionLeague.Contracts.Authentication;
using System.Net;

namespace PredictionLeague.API.Controllers;

[Route("external-auth")]
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
    public IActionResult GoogleLogin([FromQuery] string returnUrl, [FromQuery] string source)
    {
        _logger.LogInformation("Called google-login");

        // Validate and sanitise redirect URLs to prevent open redirect attacks
        var safeReturnUrl = IsValidLocalUrl(returnUrl) ? returnUrl : "/";
        var safeSource = IsValidLocalUrl(source) ? source : "/login";

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
    public async Task<IActionResult> GoogleCallback(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Called signin-google");

        var authenticateResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        var returnUrl = authenticateResult.Properties?.Items["returnUrl"] ?? "/";
        var source = authenticateResult.Properties?.Items["source"] ?? "/login";

        // Defence in depth - validate URLs again before redirect
        if (!IsValidLocalUrl(returnUrl))
        {
            _logger.LogWarning("Invalid returnUrl detected in callback: {ReturnUrl}", returnUrl);
            returnUrl = "/";
        }

        if (!IsValidLocalUrl(source))
        {
            _logger.LogWarning("Invalid source detected in callback: {Source}", source);
            source = "/login";
        }

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
        var safeReturnUrl = IsValidLocalUrl(returnUrl) ? returnUrl : "/login";
        return Redirect($"{safeReturnUrl}?error={Uri.EscapeDataString(error)}");
    }

    /// <summary>
    /// Validates that a URL is a local relative path to prevent open redirect attacks.
    /// </summary>
    private static bool IsValidLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // Only allow relative URLs starting with /
        if (!url.StartsWith('/'))
            return false;

        // Block protocol-relative URLs (//evil.com)
        if (url.StartsWith("//"))
            return false;

        // Block URLs with backslash (/\evil.com in some browsers)
        if (url.Contains('\\'))
            return false;

        return true;
    }
}
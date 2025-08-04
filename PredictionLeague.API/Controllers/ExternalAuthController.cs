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
public class ExternalAuthController : Controller
{
    private readonly ILogger<ExternalAuthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMediator _mediator;

    public ExternalAuthController(ILogger<ExternalAuthController> logger, IConfiguration configuration, IMediator mediator)
    {
        _logger = logger;
        _configuration = configuration;
        _mediator = mediator;
    }

    [HttpGet("google-login")]
    [AllowAnonymous]
    public IActionResult GoogleLogin([FromQuery] string returnUrl, [FromQuery] string source)
    {
        _logger.LogInformation("Called google-login");

        var callbackUrl = Url.Action("GoogleCallback");
        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl,
            Items =
            {
                { "returnUrl", returnUrl },
                { "source", source }
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
        var command = new LoginWithGoogleCommand(authenticateResult, source);
        var result = await _mediator.Send(command, cancellationToken);

        switch (result)
        {
            case SuccessfulAuthenticationResponse success:
                _logger.LogInformation("Google Login result was SUCCESS");

                var encodedToken = WebUtility.UrlEncode(success.RefreshTokenForCookie);
                return Redirect($"{returnUrl}?token={encodedToken}");

            case ExternalLoginFailedAuthenticationResponse failure:
                _logger.LogWarning("Google Login result was FAILURE");
                return RedirectWithError(failure.Source, failure.Message);

            default:
                _logger.LogError("Google Login result was ERROR");
                return RedirectWithError(source, "An unknown authentication error occurred.");
        }
    }

    private IActionResult RedirectWithError(string returnUrl, string error)
    {
        return Redirect($"{returnUrl}?error={Uri.EscapeDataString(error)}");
    }
}
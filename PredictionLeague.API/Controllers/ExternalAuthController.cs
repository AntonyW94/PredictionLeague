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
        return Redirect($"{returnUrl}?error={Uri.EscapeDataString(error)}");
    }
}
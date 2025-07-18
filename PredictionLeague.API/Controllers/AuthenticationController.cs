using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Authentication.Commands.Login;
using PredictionLeague.Application.Features.Authentication.Commands.LoginWithGoogle;
using PredictionLeague.Application.Features.Authentication.Commands.Logout;
using PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;
using PredictionLeague.Application.Features.Authentication.Commands.Register;
using PredictionLeague.Contracts.Authentication;
using System.Security.Claims;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(IMediator mediator, ILogger<AuthenticationController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var command = new RegisterCommand(request);

        var result = await _mediator.Send(command);
        
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request);

        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return Unauthorized(result);

        if (result.RefreshTokenForCookie != null)
            SetTokenCookie(result.RefreshTokenForCookie);

        return Ok(result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (refreshToken == null)
            return Ok();
        
        var command = new RefreshTokenCommand(refreshToken);

        var result = await _mediator.Send(command);

        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var command = new LogoutCommand(Request.Cookies["refreshToken"]);

        await _mediator.Send(command);

        Response.Cookies.Delete("refreshToken");
        
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("google-login")]
    [AllowAnonymous]
    public IActionResult GoogleLogin([FromQuery] string returnUrl, [FromQuery] string source)
    {
        var callbackUrl = Url.Action(nameof(GoogleCallback), "Authentication", new { returnUrl });
        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl,
            Items =
            {
                ["source"] = source
            }
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("signin-google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(string returnUrl)
    {
        var errorSourcePage = "/login";

        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);

            if (authenticateResult.Properties != null && authenticateResult.Properties.Items.TryGetValue("source", out var source) && source == "register")
                errorSourcePage = "/register";

            if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
            {
                _logger.LogError("External authentication with Google failed. AuthenticateResult.Succeeded was false.");
                return RedirectWithError(errorSourcePage, "Authentication with external provider failed.");
            }

            var providerKey = authenticateResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(providerKey) || !authenticateResult.Properties.Items.TryGetValue(".AuthScheme", out var provider) || string.IsNullOrEmpty(provider))
            {
                _logger.LogError("Could not determine user identifier or provider from external provider callback.");
                return RedirectWithError(errorSourcePage, "Could not determine user identifier from external provider.");
            }

            var command = new LoginWithGoogleCommand(authenticateResult.Principal, provider, providerKey);
            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
                return RedirectWithError(errorSourcePage, result.Message);

            if (result.RefreshTokenForCookie != null)
                SetTokenCookie(result.RefreshTokenForCookie);

            return RedirectWithScript(returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during Google callback processing.");
            return RedirectWithError(errorSourcePage, "An unexpected error occurred. Please try again.");
        }
    }

    private void SetTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(7),
            Secure = true,
            SameSite = SameSiteMode.Strict
        };
        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }

    private IActionResult RedirectWithError(string returnUrl, string error)
    {
        var redirectUrl = $"{returnUrl}?error={Uri.EscapeDataString(error)}";
        return Redirect(redirectUrl);
    }

    private IActionResult RedirectWithScript(string returnUrl)
    {
        var script = $"<html><body><script>window.location = '{returnUrl}';</script></body></html>";
        return Content(script, "text/html");
    }
}
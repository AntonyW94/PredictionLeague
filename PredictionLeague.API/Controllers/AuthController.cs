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

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(request.FirstName, request.LastName, request.Email, request.Password);
        var result = await _mediator.Send(command, cancellationToken);

        if (result is not SuccessfulAuthenticationResponse success)
            return result.IsSuccess ? Ok(result) : BadRequest(result);

        SetTokenCookie(success.RefreshTokenForCookie);
        return Ok(success);

    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, cancellationToken);

        if (result is not SuccessfulAuthenticationResponse success)
            return Unauthorized(result);

        SetTokenCookie(success.RefreshTokenForCookie);
        return Ok(result);

    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Called refresh-token");

        var refreshToken = Request.Cookies["refreshToken"];
        if (refreshToken == null)
        {
            _logger.LogInformation("Refresh token is missing");
            return Ok(new { message = "Refresh token is missing." });
        }

        var command = new RefreshTokenCommand(refreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        if (result is not SuccessfulAuthenticationResponse success)
        {
            if (result is FailedAuthenticationResponse failedResponse)
            {
                _logger.LogError("Refresh Token Command failed with message: {Message}", failedResponse.Message);
            }
            else
            {
                _logger.LogError("Refresh Token Command failed with unknown error");
            }

            return BadRequest(result);
        }

        SetTokenCookie(success.RefreshTokenForCookie);
        return Ok(success);

    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var command = new LogoutCommand(CurrentUserId);
        await _mediator.Send(command, cancellationToken);

        Response.Cookies.Delete("refreshToken");

        return NoContent();
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

    [HttpGet("signin-google", Name = "GoogleCallback")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallbackAsync(CancellationToken cancellationToken)
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
                _logger.LogWarning("Google Login result was SUCCESS");
                SetTokenCookie(success.RefreshTokenForCookie);
                
                return Redirect(returnUrl);

            case ExternalLoginFailedAuthenticationResponse failure:
                _logger.LogWarning("Google Login result was FAILURE");
                return RedirectWithError(failure.Source, failure.Message);

            default:
                _logger.LogError("Google Login result was ERROR");
                return RedirectWithError(source, "An unknown authentication error occurred.");
        }
    }

    private void SetTokenCookie(string token)
    {
        _logger.LogInformation("Setting 'refreshToken' cookie.");

        var expiryDays = double.Parse(_configuration["JwtSettings:RefreshTokenExpiryDays"]!);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(expiryDays),
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
        };

        try
        {
            Response.Cookies.Append("refreshToken", token, cookieOptions);
            _logger.LogInformation("Set 'refreshToken' cookie.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set 'refreshToken' cookie.");
            throw new InvalidOperationException("Failed to set 'refreshToken' cookie.", ex);
        }

    }

    private IActionResult RedirectWithError(string returnUrl, string error)
    {
        var redirectUrl = $"{returnUrl}?error={Uri.EscapeDataString(error)}";
        return Redirect(redirectUrl);
    }

    private IActionResult RedirectWithScript(string returnUrl)
    {
        var script = $@"
        <html>
            <body>
                <script>
                    if (window.opener) {{
                        window.opener.location = '{returnUrl}';
                        window.close();
                    }} else {{
                        window.location = '{returnUrl}';
                    }}
                </script>
            </body>
        </html>";

        return Content(script, "text/html");
    }
}
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionLeague.Application.Features.Authentication.Commands.Login;
using PredictionLeague.Application.Features.Authentication.Commands.Logout;
using PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;
using PredictionLeague.Application.Features.Authentication.Commands.Register;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AuthController : ApiControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public AuthController(ILogger<AuthController> logger, IConfiguration configuration, IMediator mediator)
    {
        _logger = logger;
        _configuration = configuration;
        _mediator = mediator;
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
   
    public class RefreshTokenRequest
    {
        public string? Token { get; set; }
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Refresh-Token Endpoint Called ---");

        // This endpoint now handles both flows:
        // 1. External Login: Token comes in the request body.
        // 2. Standard Refresh: Token comes from the HttpOnly cookie.

        var refreshToken = request?.Token;
        string tokenSource;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            tokenSource = "RequestBody";
            _logger.LogInformation("Refresh token found in request body.");
        }
        else
        {
            refreshToken = Request.Cookies["refreshToken"];
            tokenSource = "Cookie";
            _logger.LogInformation("Refresh token not found in body, checking cookie.");
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("Refresh token is missing from both request body and cookie. Cannot authenticate.");
            return BadRequest(new { message = "Refresh token is missing." });
        }

        _logger.LogInformation("Processing refresh token from {TokenSource}. Token (first 10 chars): {TokenStart}", tokenSource, refreshToken.Length > 10 ? refreshToken.Substring(0, 10) : refreshToken);

        var command = new RefreshTokenCommand(refreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        if (result is not SuccessfulAuthenticationResponse success)
        {
            _logger.LogError("Refresh Token Command failed. Result: {@Result}", result);
            return BadRequest(result);
        }

        _logger.LogInformation("Refresh Token Command was successful. Setting new refresh token cookie for user.");
       
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
            Path = "/",
            Domain = ".thepredictions.co.uk"
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
}
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PredictionLeague.Application.Features.Authentication.Commands.Login;
using PredictionLeague.Application.Features.Authentication.Commands.Logout;
using PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;
using PredictionLeague.Application.Features.Authentication.Commands.Register;
using PredictionLeague.Contracts.Authentication;
using Swashbuckle.AspNetCore.Annotations;

namespace PredictionLeague.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("auth")]
[SwaggerTag("Authentication - Register, login, logout, and token refresh")]
public class AuthController : AuthControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IMediator _mediator;

    public AuthController(ILogger<AuthController> logger, IConfiguration configuration, IMediator mediator) : base(configuration)
    {
        _logger = logger;
        _mediator = mediator;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Register a new user account",
        Description = "Creates a new user account with email and password. Returns authentication tokens on success. The user is automatically logged in after registration.")]
    [SwaggerResponse(200, "Registration successful - returns access token, refresh token, and user details", typeof(AuthenticationResponse))]
    [SwaggerResponse(400, "Validation failed - email already exists, password too weak, or invalid input")]
    public async Task<IActionResult> RegisterAsync(
        [FromBody, SwaggerParameter("Registration details including email, password, first name, and last name", Required = true)] RegisterRequest request,
        CancellationToken cancellationToken)
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
    [SwaggerOperation(
        Summary = "Authenticate with email and password",
        Description = "Validates credentials and returns authentication tokens. Access token expires in 15 minutes. Refresh token is set as HTTP-only cookie and also returned in response body.")]
    [SwaggerResponse(200, "Login successful - returns access token, refresh token, and user details", typeof(AuthenticationResponse))]
    [SwaggerResponse(400, "Invalid credentials or account locked")]
    public async Task<IActionResult> LoginAsync(
        [FromBody, SwaggerParameter("Login credentials", Required = true)] LoginRequest request,
        CancellationToken cancellationToken)
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
    [SwaggerOperation(
        Summary = "Refresh an expired access token",
        Description = "Uses the refresh token (from HTTP-only cookie or request body) to obtain a new access token. The old refresh token is invalidated and a new one is issued (token rotation).")]
    [SwaggerResponse(200, "Token refresh successful - returns new access token and refresh token", typeof(AuthenticationResponse))]
    [SwaggerResponse(400, "Invalid or expired refresh token")]
    public async Task<IActionResult> RefreshTokenAsync(
        [FromBody, SwaggerParameter("Refresh token request (token can also be read from cookie)", Required = false)] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Refresh-Token Endpoint Called ---");

        var refreshToken = request.Token;
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

        _logger.LogInformation("Processing refresh token from {TokenSource}", tokenSource);

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
    [SwaggerOperation(
        Summary = "Log out the current user",
        Description = "Invalidates the current refresh token and clears the refresh token cookie. The access token remains valid until expiry but should be discarded by the client.")]
    [SwaggerResponse(200, "Logout successful")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var command = new LogoutCommand(CurrentUserId);
        await _mediator.Send(command, cancellationToken);

        Response.Cookies.Delete("refreshToken");

        return NoContent();
    }
}
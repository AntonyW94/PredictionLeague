using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PredictionLeague.Application.Features.Authentication.Commands.Login;
using PredictionLeague.Application.Features.Authentication.Commands.Logout;
using PredictionLeague.Application.Features.Authentication.Commands.RefreshToken;
using PredictionLeague.Application.Features.Authentication.Commands.Register;
using PredictionLeague.Application.Features.Authentication.Commands.RequestPasswordReset;
using PredictionLeague.Application.Features.Authentication.Commands.ResetPassword;
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
    [SwaggerOperation(
        Summary = "Authenticate with email and password",
        Description = "Validates credentials and returns authentication tokens. Access token expires in 15 minutes. Refresh token is set as HTTP-only cookie and also returned in response body.")]
    [SwaggerResponse(200, "Login successful - returns access token, refresh token, and user details", typeof(AuthenticationResponse))]
    [SwaggerResponse(401, "Invalid credentials or account locked")]
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
    [SwaggerOperation(
        Summary = "Log out the current user",
        Description = "Invalidates the current refresh token and clears the refresh token cookie. The access token remains valid until expiry but should be discarded by the client.")]
    [SwaggerResponse(204, "Logout successful")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var command = new LogoutCommand(CurrentUserId);
        await _mediator.Send(command, cancellationToken);

        Response.Cookies.Delete("refreshToken");

        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Request a password reset email",
        Description = "Sends a password reset email if the account exists. For security, always returns success regardless of whether the email exists. Rate limited to prevent abuse.")]
    [SwaggerResponse(200, "Request processed - check email if account exists")]
    [SwaggerResponse(400, "Validation failed - invalid email format")]
    [SwaggerResponse(429, "Too many requests - rate limit exceeded")]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody, SwaggerParameter("Email address for password reset", Required = true)] RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        // Build the reset URL base from the request
        // This allows the client URL to be determined dynamically
        var resetUrlBase = $"{Request.Headers["Origin"]}/authentication/reset-password";

        var command = new RequestPasswordResetCommand(request.Email, resetUrlBase);
        await _mediator.Send(command, cancellationToken);

        // Always return OK to prevent email enumeration
        return Ok(new { message = "If an account exists with that email, you'll receive a password reset link shortly." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Reset password using token from email",
        Description = "Validates the reset token and updates the password. On success, returns authentication tokens (auto-login). Tokens expire after 1 hour.")]
    [SwaggerResponse(200, "Password reset successful - returns authentication tokens", typeof(SuccessfulResetPasswordResponse))]
    [SwaggerResponse(400, "Validation failed or token invalid/expired")]
    [SwaggerResponse(429, "Too many requests - rate limit exceeded")]
    public async Task<IActionResult> ResetPasswordAsync(
        [FromBody, SwaggerParameter("Reset password details including token and new password", Required = true)] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(
            request.Token,
            request.NewPassword
        );

        var result = await _mediator.Send(command, cancellationToken);

        if (result is not SuccessfulResetPasswordResponse success)
            return BadRequest(result);

        SetTokenCookie(success.RefreshTokenForCookie);
        return Ok(success);
    }
}

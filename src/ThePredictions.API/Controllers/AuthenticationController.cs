using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using ThePredictions.Application.Features.Authentication.Commands.ConfirmEmail;
using ThePredictions.Application.Features.Authentication.Commands.Login;
using ThePredictions.Application.Features.Authentication.Commands.Logout;
using ThePredictions.Application.Features.Authentication.Commands.ResendConfirmation;
using ThePredictions.Application.Features.Authentication.Commands.RefreshToken;
using ThePredictions.Application.Features.Authentication.Commands.Register;
using ThePredictions.Application.Features.Authentication.Commands.RequestPasswordReset;
using ThePredictions.Application.Features.Authentication.Commands.ResetPassword;
using ThePredictions.Contracts.Authentication;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[SwaggerTag("Authentication - Register, login, logout, and token refresh")]
public class AuthenticationController(ILogger<AuthenticationController> logger, IConfiguration configuration, IMediator mediator) : AuthControllerBase(configuration)
{
    // Rate limiting is applied per-endpoint: the strict "auth" policy guards
    // credential / email-sending endpoints, while the silent, cookie-authenticated
    // refresh and logout use the standard "api" policy so a normal session's
    // periodic refreshes aren't throttled like password-guessing attempts.
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
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
        var confirmUrlBase = $"{Request.Headers["Origin"]}/authentication/confirm-email";

        var command = new RegisterCommand(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            request.MarketingOptIn,
            confirmUrlBase);
        var result = await mediator.Send(command, cancellationToken);

        if (result is not SuccessfulAuthenticationResponse success)
            return result.IsSuccess ? Ok(result) : BadRequest(result);

        SetTokenCookie(success.RefreshTokenForCookie);
        return Ok(success);

    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Confirm an email address using the token from the confirmation email",
        Description = "Validates the confirmation token and marks the email as verified. Tokens expire after 72 hours.")]
    [SwaggerResponse(200, "Email confirmed")]
    [SwaggerResponse(400, "Token invalid or expired")]
    public async Task<IActionResult> ConfirmEmailAsync(
        [FromBody, SwaggerParameter("Confirmation token from the email link", Required = true)] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ConfirmEmailCommand(request.Token), cancellationToken);
        return Ok(new { message = "Your email address has been confirmed. Thanks!" });
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Resend the email-confirmation link to the current user",
        Description = "Issues a fresh confirmation link if the user's email is not yet verified. Rate limited; always returns success.")]
    [SwaggerResponse(200, "Request processed")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> ResendConfirmationAsync(CancellationToken cancellationToken)
    {
        var confirmUrlBase = $"{Request.Headers["Origin"]}/authentication/confirm-email";

        await mediator.Send(new ResendConfirmationCommand(CurrentUserId, confirmUrlBase), cancellationToken);
        return Ok(new { message = "If your email still needs confirming, a new link is on its way." });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
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
        var result = await mediator.Send(command, cancellationToken);

        if (result is not SuccessfulAuthenticationResponse success)
            return Unauthorized(result);

        SetTokenCookie(success.RefreshTokenForCookie);
        return Ok(result);

    }

    [HttpPost("refresh-token")]
    [EnableRateLimiting("api")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Refresh an expired access token",
        Description = "Uses the refresh token (from HTTP-only cookie or request body) to obtain a new access token. The old refresh token is invalidated and a new one is issued (token rotation).")]
    [SwaggerResponse(200, "Token refresh successful - returns new access token and refresh token", typeof(AuthenticationResponse))]
    [SwaggerResponse(400, "Invalid or expired refresh token")]
    public async Task<IActionResult> RefreshTokenAsync(
        // EmptyBodyBehavior.Allow (and the nullable type) are essential: the normal
        // silent refresh sends an EMPTY body and carries the token in the HTTP-only
        // cookie. Without this, [ApiController] rejects the empty body with an
        // automatic 400 ("A non-empty request body is required") before the action
        // runs, so the cookie fallback below never executes and every cookie-based
        // refresh fails - logging the user out when their access token expires.
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow), SwaggerParameter("Refresh token request (token can also be read from cookie)", Required = false)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("--- Refresh-Token Endpoint Called ---");

        var refreshToken = request?.Token;
        string tokenSource;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            tokenSource = "RequestBody";
            logger.LogInformation("Refresh token found in request body.");
        }
        else
        {
            refreshToken = Request.Cookies["refreshToken"];
            tokenSource = "Cookie";
            logger.LogInformation("Refresh token not found in body, checking cookie.");
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            logger.LogWarning("Refresh token is missing from both request body and cookie. Cannot authenticate.");
            return BadRequest(new { message = "Refresh token is missing." });
        }

        logger.LogInformation("Processing refresh token from {TokenSource}", tokenSource);

        var command = new RefreshTokenCommand(refreshToken);
        var result = await mediator.Send(command, cancellationToken);

        if (result is not SuccessfulAuthenticationResponse success)
        {
            logger.LogError("Refresh Token Command failed. Result: {@Result}", result);
            return BadRequest(result);
        }

        logger.LogInformation("Refresh Token Command was successful. Setting new refresh token cookie for user.");

        SetTokenCookie(success.RefreshTokenForCookie);
        return Ok(success);
    }

    [HttpPost("logout")]
    [EnableRateLimiting("api")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Log out the current user",
        Description = "Invalidates the current refresh token and clears the refresh token cookie. The access token remains valid until expiry but should be discarded by the client.")]
    [SwaggerResponse(204, "Logout successful")]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        var command = new LogoutCommand(CurrentUserId, refreshToken);
        await mediator.Send(command, cancellationToken);

        DeleteTokenCookie();

        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
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
        await mediator.Send(command, cancellationToken);

        // Always return OK to prevent email enumeration
        return Ok(new { message = "If an account exists with that email, you'll receive a password reset link shortly." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
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

        var result = await mediator.Send(command, cancellationToken);

        if (result is not SuccessfulResetPasswordResponse success)
            return BadRequest(result);

        SetTokenCookie(success.RefreshTokenForCookie);
        return Ok(success);
    }
}

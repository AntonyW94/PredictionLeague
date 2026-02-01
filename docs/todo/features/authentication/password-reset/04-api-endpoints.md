# Task 4: API Endpoints

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add two new endpoints to `AuthController` for requesting password reset and resetting the password. Both endpoints are anonymous (no authentication required) and use the existing `auth` rate limiting policy.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.API/Controllers/AuthController.cs` | Modify | Add two new endpoints |

## Implementation Steps

### Step 1: Add Request Password Reset Endpoint

Add this method to `AuthController.cs`:

```csharp
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
```

### Step 2: Add Reset Password Endpoint

Add this method to `AuthController.cs`:

```csharp
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
```

**Note:** The command only takes `Token` and `NewPassword` - no email parameter. The user is looked up from the token in the database.

### Step 3: Add Required Using Statements

Ensure these using statements are at the top of `AuthController.cs`:

```csharp
using PredictionLeague.Application.Features.Authentication.Commands.RequestPasswordReset;
using PredictionLeague.Application.Features.Authentication.Commands.ResetPassword;
using PredictionLeague.Contracts.Authentication;
```

## Code Patterns to Follow

### Anonymous Endpoint Pattern

Follow the existing pattern from `LoginAsync` and `RegisterAsync`:

```csharp
[HttpPost("endpoint-name")]
[AllowAnonymous]  // No authentication required
[SwaggerOperation(...)]
[SwaggerResponse(...)]
public async Task<IActionResult> MethodAsync(...)
{
    // ...
}
```

### Token Cookie Pattern

Follow the existing pattern for setting refresh token cookies:

```csharp
if (result is not SuccessfulResetPasswordResponse success)
    return BadRequest(result);

SetTokenCookie(success.RefreshTokenForCookie);  // Inherited from AuthControllerBase
return Ok(success);
```

### MediatR Command Pattern

```csharp
var command = new SomeCommand(param1, param2);
var result = await _mediator.Send(command, cancellationToken);
```

### Origin Header for Reset URL

The `Origin` header is used to construct the reset URL dynamically:

```csharp
var resetUrlBase = $"{Request.Headers["Origin"]}/authentication/reset-password";
```

This allows the API to work with different client URLs (localhost, staging, production).

## Complete AuthController After Changes

The endpoints section should look like this:

```csharp
// Existing endpoints
[HttpPost("register")] ...
[HttpPost("login")] ...
[HttpPost("refresh-token")] ...
[HttpPost("logout")] ...

// New endpoints
[HttpPost("forgot-password")] ...
[HttpPost("reset-password")] ...
```

## Verification

- [ ] `POST /api/auth/forgot-password` endpoint accessible without authentication
- [ ] `POST /api/auth/reset-password` endpoint accessible without authentication
- [ ] Both endpoints use `[EnableRateLimiting("auth")]` (inherited from controller)
- [ ] Forgot password always returns 200 with generic message
- [ ] Reset password returns 200 with tokens on success
- [ ] Reset password returns 400 on invalid token
- [ ] Reset password sets refresh token cookie on success
- [ ] Reset password request has no email field (token-only)
- [ ] Swagger documentation displays correctly
- [ ] Solution compiles without errors

## Edge Cases to Consider

- **Missing Origin header** → Could fallback to configuration-based URL or return error
- **CORS preflight** → Handled by existing CORS configuration
- **Rate limit exceeded** → Returns 429 (handled by rate limiting middleware)
- **Invalid JSON body** → Returns 400 (handled by model binding)

## Notes

- The `auth` rate limiting policy (10 requests per 5 minutes per IP) is applied at the controller level
- Additional per-user rate limiting (3 per hour) is handled in the command handler
- The `SetTokenCookie` method is inherited from `AuthControllerBase`
- No changes needed to `AuthControllerBase` - it already has the cookie method
- The Swagger attributes provide good API documentation for frontend developers
- The reset endpoint does NOT require email - this is a security improvement to avoid exposing email in URLs

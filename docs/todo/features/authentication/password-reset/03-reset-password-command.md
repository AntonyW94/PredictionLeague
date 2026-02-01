# Task 3: Reset Password Command

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a command and handler that validates the reset token and updates the user's password. On success, automatically logs the user in by generating authentication tokens.

## Files to Create

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Authentication/Commands/ResetPassword/ResetPasswordCommand.cs` | Create | Command record |
| `PredictionLeague.Application/Features/Authentication/Commands/ResetPassword/ResetPasswordCommandHandler.cs` | Create | Command handler |
| `PredictionLeague.Contracts/Authentication/ResetPasswordRequest.cs` | Create | API request DTO |
| `PredictionLeague.Validators/Authentication/ResetPasswordRequestValidator.cs` | Create | FluentValidation validator |

## Implementation Steps

### Step 1: Create the Command

```csharp
// ResetPasswordCommand.cs

using MediatR;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword
) : IRequest<ResetPasswordResponse>;
```

### Step 2: Create the Response Types

```csharp
// ResetPasswordResponse.cs (in PredictionLeague.Contracts/Authentication)

namespace PredictionLeague.Contracts.Authentication;

public abstract record ResetPasswordResponse(bool IsSuccess, string? Message = null);

public record SuccessfulResetPasswordResponse(
    string AccessToken,
    string RefreshTokenForCookie,
    DateTime ExpiresAtUtc
) : ResetPasswordResponse(true);

public record FailedResetPasswordResponse(string Message) : ResetPasswordResponse(false, Message);
```

### Step 3: Create the Request DTO

```csharp
// ResetPasswordRequest.cs

namespace PredictionLeague.Contracts.Authentication;

public record ResetPasswordRequest
{
    public string Email { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
```

### Step 4: Create the Validator

```csharp
// ResetPasswordRequestValidator.cs

using FluentValidation;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Validators.Authentication;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Please enter a valid email address");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Please confirm your password")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match");
    }
}
```

### Step 5: Create the Command Handler

```csharp
// ResetPasswordCommandHandler.cs

using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    private readonly IUserManager _userManager;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IUserManager userManager,
        IAuthenticationTokenService tokenService,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<ResetPasswordResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            _logger.LogWarning("Password reset attempted for non-existent email: {Email}", request.Email);
            return new FailedResetPasswordResponse("The password reset link is invalid or has expired.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Password reset failed for User (ID: {UserId}). Errors: {Errors}",
                user.Id, string.Join(", ", result.Errors));

            // Don't expose specific error details - could reveal token validity
            return new FailedResetPasswordResponse("The password reset link is invalid or has expired.");
        }

        _logger.LogInformation("Password successfully reset for User (ID: {UserId})", user.Id);

        // Auto-login: Generate tokens for the user
        var (accessToken, refreshToken, expiresAtUtc) = await _tokenService.GenerateTokensAsync(user, cancellationToken);

        return new SuccessfulResetPasswordResponse(
            AccessToken: accessToken,
            RefreshTokenForCookie: refreshToken,
            ExpiresAtUtc: expiresAtUtc
        );
    }
}
```

## Code Patterns to Follow

### Authentication Response Pattern

Follow the existing pattern used by `LoginCommand` and `RegisterCommand`:

```csharp
// Abstract base with IsSuccess
public abstract record ResetPasswordResponse(bool IsSuccess, string? Message = null);

// Success includes tokens
public record SuccessfulResetPasswordResponse(...) : ResetPasswordResponse(true);

// Failure includes message
public record FailedResetPasswordResponse(string Message) : ResetPasswordResponse(false, Message);
```

### Token Generation Pattern

Use the existing `IAuthenticationTokenService.GenerateTokensAsync`:

```csharp
var (accessToken, refreshToken, expiresAtUtc) = await _tokenService.GenerateTokensAsync(user, cancellationToken);
```

### Password Validation

Match the existing password requirements from `DependencyInjection.cs`:

```csharp
options.Password.RequiredLength = 8;
options.Password.RequireDigit = true;
options.Password.RequireLowercase = true;
options.Password.RequireUppercase = true;
options.Password.RequireNonAlphanumeric = false;
options.Password.RequiredUniqueChars = 4;
```

Note: The `RequiredUniqueChars = 4` requirement is enforced by Identity, not by FluentValidation.

## Verification

- [ ] Command compiles with correct record structure
- [ ] Handler returns `FailedResetPasswordResponse` for non-existent user
- [ ] Handler returns `FailedResetPasswordResponse` for invalid/expired token
- [ ] Handler returns `SuccessfulResetPasswordResponse` with tokens on success
- [ ] Validator enforces password requirements
- [ ] Validator ensures ConfirmPassword matches NewPassword
- [ ] Logging follows `(ID: {UserId})` pattern
- [ ] Error messages don't reveal whether email exists or token is expired vs invalid

## Edge Cases to Consider

- **User not found** → Return generic "invalid or expired" message
- **Token expired** → Identity returns failure, return generic message
- **Token already used** → Identity returns failure (SecurityStamp changed)
- **Password doesn't meet requirements** → Identity validates and returns errors
- **Confirm password mismatch** → Validator catches before handler

## Notes

- The generic error message "invalid or has expired" prevents attackers from determining:
  - Whether the email exists in the system
  - Whether the token was expired vs never valid
  - Whether the token was already used
- Auto-login after password reset improves UX - user doesn't need to re-enter credentials
- The `IAuthenticationTokenService` is already injected in `RegisterCommandHandler`, follow same pattern
- Identity's `ResetPasswordAsync` handles all token validation internally

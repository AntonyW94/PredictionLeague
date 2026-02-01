# Task 3: Reset Password Command

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a command and handler that validates the reset token from the database, looks up the user, and updates their password. On success, deletes the token and automatically logs the user in by generating authentication tokens.

## Files to Create

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Authentication/Commands/ResetPassword/ResetPasswordCommand.cs` | Create | Command record |
| `PredictionLeague.Application/Features/Authentication/Commands/ResetPassword/ResetPasswordCommandHandler.cs` | Create | Command handler |
| `PredictionLeague.Contracts/Authentication/ResetPasswordRequest.cs` | Create | API request DTO |
| `PredictionLeague.Contracts/Authentication/ResetPasswordResponse.cs` | Create | API response DTOs |
| `PredictionLeague.Validators/Authentication/ResetPasswordRequestValidator.cs` | Create | FluentValidation validator |

## Implementation Steps

### Step 1: Create the Command

```csharp
// ResetPasswordCommand.cs

using MediatR;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Token,
    string NewPassword
) : IRequest<ResetPasswordResponse>;
```

**Note:** No email parameter - the user is looked up from the token.

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
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
```

**Note:** No email field - token-only approach.

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
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Application.Features.Authentication.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IUserManager _userManager;
    private readonly IAuthenticationTokenService _tokenService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository tokenRepository,
        IUserManager userManager,
        IAuthenticationTokenService tokenService,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _tokenRepository = tokenRepository;
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<ResetPasswordResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Clean up expired tokens (opportunistic cleanup)
        await _tokenRepository.DeleteExpiredTokensAsync(cancellationToken);

        // Look up the token
        var resetToken = await _tokenRepository.GetByTokenAsync(request.Token, cancellationToken);

        if (resetToken == null)
        {
            _logger.LogWarning("Password reset attempted with non-existent token");
            return new FailedResetPasswordResponse("The password reset link is invalid or has expired.");
        }

        if (resetToken.IsExpired)
        {
            _logger.LogWarning("Password reset attempted with expired token for User (ID: {UserId})", resetToken.UserId);
            await _tokenRepository.DeleteAsync(request.Token, cancellationToken);
            return new FailedResetPasswordResponse("The password reset link is invalid or has expired.");
        }

        // Look up the user
        var user = await _userManager.FindByIdAsync(resetToken.UserId);

        if (user == null)
        {
            _logger.LogWarning("Password reset token references non-existent User (ID: {UserId})", resetToken.UserId);
            await _tokenRepository.DeleteAsync(request.Token, cancellationToken);
            return new FailedResetPasswordResponse("The password reset link is invalid or has expired.");
        }

        // Reset the password using Identity's password hasher
        var result = await _userManager.ResetPasswordDirectAsync(user, request.NewPassword);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Password reset failed for User (ID: {UserId}). Errors: {Errors}",
                user.Id, string.Join(", ", result.Errors));

            // Return the first error (usually password policy violation)
            var errorMessage = result.Errors.FirstOrDefault() ?? "Password reset failed.";
            return new FailedResetPasswordResponse(errorMessage);
        }

        // Delete the used token (and any other tokens for this user)
        await _tokenRepository.DeleteByUserIdAsync(user.Id, cancellationToken);

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

### Step 6: Add ResetPasswordDirectAsync to IUserManager

Since we're not using ASP.NET Identity's token validation (we have our own), we need a method to reset the password directly:

```csharp
// In IUserManager.cs, add to #region Update

Task<UserManagerResult> ResetPasswordDirectAsync(ApplicationUser user, string newPassword);
```

```csharp
// In UserManagerService.cs, add to #region Update

public async Task<UserManagerResult> ResetPasswordDirectAsync(ApplicationUser user, string newPassword)
{
    // Remove existing password (if any) and add new one
    var removeResult = await _userManager.RemovePasswordAsync(user);
    if (!removeResult.Succeeded)
    {
        return UserManagerResult.Failure(removeResult.Errors.Select(e => e.Description));
    }

    var addResult = await _userManager.AddPasswordAsync(user, newPassword);
    return addResult.Succeeded
        ? UserManagerResult.Success()
        : UserManagerResult.Failure(addResult.Errors.Select(e => e.Description));
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

ASP.NET Identity validates the password against the configured policy when `AddPasswordAsync` is called:

```csharp
options.Password.RequiredLength = 8;
options.Password.RequireDigit = true;
options.Password.RequireLowercase = true;
options.Password.RequireUppercase = true;
options.Password.RequireNonAlphanumeric = false;
options.Password.RequiredUniqueChars = 4;
```

## Verification

- [ ] Command compiles with token-only approach (no email)
- [ ] Handler looks up user from token in database
- [ ] Handler returns `FailedResetPasswordResponse` for non-existent token
- [ ] Handler returns `FailedResetPasswordResponse` for expired token
- [ ] Handler returns `FailedResetPasswordResponse` for non-existent user
- [ ] Handler returns `SuccessfulResetPasswordResponse` with tokens on success
- [ ] Token is deleted after successful password reset
- [ ] All tokens for user are deleted after successful reset
- [ ] Expired tokens are cleaned up opportunistically
- [ ] Validator enforces password requirements
- [ ] Validator ensures ConfirmPassword matches NewPassword
- [ ] Logging follows `(ID: {UserId})` pattern
- [ ] Error messages don't reveal specifics (generic "invalid or expired")

## Edge Cases to Consider

- **Token not found** → Return generic "invalid or expired" message
- **Token expired** → Delete token, return generic message
- **User deleted after token created** → Delete token, return generic message
- **Password doesn't meet requirements** → Identity validates and returns errors
- **Confirm password mismatch** → Validator catches before handler
- **Multiple valid tokens** → All deleted after successful reset

## Notes

- The generic error message "invalid or has expired" prevents attackers from determining:
  - Whether the token ever existed
  - Whether the token was expired vs never valid
  - Whether the token was already used
- Auto-login after password reset improves UX - user doesn't need to re-enter credentials
- The `IAuthenticationTokenService` is already injected in `RegisterCommandHandler`, follow same pattern
- `ResetPasswordDirectAsync` bypasses Identity's token validation since we use our own tokens
- Deleting all user tokens after reset ensures old links stop working

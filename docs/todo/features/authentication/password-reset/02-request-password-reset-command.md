# Task 2: Request Password Reset Command

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create a command and handler that processes password reset requests. The handler looks up the user, determines if they have a password or use Google sign-in, and sends the appropriate email. Always returns success to prevent email enumeration.

## Files to Create

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Authentication/Commands/RequestPasswordReset/RequestPasswordResetCommand.cs` | Create | Command record |
| `PredictionLeague.Application/Features/Authentication/Commands/RequestPasswordReset/RequestPasswordResetCommandHandler.cs` | Create | Command handler |
| `PredictionLeague.Contracts/Authentication/RequestPasswordResetRequest.cs` | Create | API request DTO |
| `PredictionLeague.Validators/Authentication/RequestPasswordResetRequestValidator.cs` | Create | FluentValidation validator |

## Implementation Steps

### Step 1: Create the Command

```csharp
// RequestPasswordResetCommand.cs

using MediatR;

namespace PredictionLeague.Application.Features.Authentication.Commands.RequestPasswordReset;

public record RequestPasswordResetCommand(string Email, string ResetUrlBase) : IRequest<Unit>;
```

### Step 2: Create the Request DTO

```csharp
// RequestPasswordResetRequest.cs

namespace PredictionLeague.Contracts.Authentication;

public record RequestPasswordResetRequest
{
    public string Email { get; init; } = string.Empty;
}
```

### Step 3: Create the Validator

```csharp
// RequestPasswordResetRequestValidator.cs

using FluentValidation;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Validators.Authentication;

public class RequestPasswordResetRequestValidator : AbstractValidator<RequestPasswordResetRequest>
{
    public RequestPasswordResetRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Please enter a valid email address");
    }
}
```

### Step 4: Create the Command Handler

```csharp
// RequestPasswordResetCommandHandler.cs

using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PredictionLeague.Application.Configuration;
using PredictionLeague.Application.Services;
using System.Web;

namespace PredictionLeague.Application.Features.Authentication.Commands.RequestPasswordReset;

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, Unit>
{
    private readonly IUserManager _userManager;
    private readonly IEmailService _emailService;
    private readonly BrevoSettings _brevoSettings;
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

    public RequestPasswordResetCommandHandler(
        IUserManager userManager,
        IEmailService emailService,
        IOptions<BrevoSettings> brevoSettings,
        ILogger<RequestPasswordResetCommandHandler> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _brevoSettings = brevoSettings.Value;
        _logger = logger;
    }

    public async Task<Unit> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            // Security: Don't reveal that email doesn't exist
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
            return Unit.Value;
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);

        if (hasPassword)
        {
            await SendPasswordResetEmailAsync(user, request.ResetUrlBase);
        }
        else
        {
            await SendGoogleUserEmailAsync(user, request.ResetUrlBase);
        }

        return Unit.Value;
    }

    private async Task SendPasswordResetEmailAsync(Domain.Models.ApplicationUser user, string resetUrlBase)
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = HttpUtility.UrlEncode(token);
        var encodedEmail = HttpUtility.UrlEncode(user.Email);

        var resetLink = $"{resetUrlBase}?token={encodedToken}&email={encodedEmail}";

        var templateId = _brevoSettings.Templates?.PasswordReset
            ?? throw new InvalidOperationException("PasswordReset email template ID is not configured");

        await _emailService.SendTemplatedEmailAsync(
            user.Email!,
            templateId,
            new
            {
                firstName = user.FirstName,
                resetLink
            });

        _logger.LogInformation("Password reset email sent to User (ID: {UserId})", user.Id);
    }

    private async Task SendGoogleUserEmailAsync(Domain.Models.ApplicationUser user, string resetUrlBase)
    {
        // Extract base URL (remove the reset-password path)
        var baseUrl = resetUrlBase.Replace("/authentication/reset-password", "");
        var loginLink = $"{baseUrl}/authentication/login";

        var templateId = _brevoSettings.Templates?.PasswordResetGoogleUser
            ?? throw new InvalidOperationException("PasswordResetGoogleUser email template ID is not configured");

        await _emailService.SendTemplatedEmailAsync(
            user.Email!,
            templateId,
            new
            {
                firstName = user.FirstName,
                loginLink
            });

        _logger.LogInformation("Google sign-in reminder email sent to User (ID: {UserId})", user.Id);
    }
}
```

## Code Patterns to Follow

### Logging Format

Follow the project's logging convention with `(ID: {EntityId})`:

```csharp
_logger.LogInformation("Password reset email sent to User (ID: {UserId})", user.Id);
```

### Email Service Pattern

Use the existing `IEmailService.SendTemplatedEmailAsync` pattern:

```csharp
await _emailService.SendTemplatedEmailAsync(
    recipientEmail,
    templateId,
    new { param1 = value1, param2 = value2 }
);
```

### Security: No Email Enumeration

Always return success (`Unit.Value`) regardless of whether the email exists:

```csharp
if (user == null)
{
    _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
    return Unit.Value;  // Don't reveal email doesn't exist
}
```

## Verification

- [ ] Command compiles and follows `IRequest<Unit>` pattern
- [ ] Handler injects all required dependencies
- [ ] Handler returns `Unit.Value` even when user not found (security)
- [ ] Password users receive reset email with valid token
- [ ] Google-only users receive "use Google sign-in" email
- [ ] Token is URL-encoded in the reset link
- [ ] Email is URL-encoded in the reset link
- [ ] Logging follows `(ID: {UserId})` pattern
- [ ] Validator rejects empty and invalid emails

## Edge Cases to Consider

- **Email not found** → Log and return success (no email sent)
- **User has both password AND Google** → Has password, so send reset email
- **Email service throws** → Let exception bubble up (will be caught by global handler)
- **Template ID not configured** → Throw `InvalidOperationException` with clear message
- **Email with special characters** → URL encoding handles this

## Notes

- The `ResetUrlBase` is passed from the API controller, which knows the client's base URL
- The token generated by Identity can be quite long; URL encoding ensures it's safe
- Rate limiting is handled at the API layer, not in this handler
- MediatR automatically registers this handler via assembly scanning

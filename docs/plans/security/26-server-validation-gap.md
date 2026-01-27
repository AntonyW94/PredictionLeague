# P2: Server-Side Validation Gap

## Summary

**Severity:** P2 - Medium
**Type:** Input Validation Bypass
**CWE:** CWE-20 (Improper Input Validation)
**OWASP:** A03:2021 - Injection (related)

## Description

FluentValidation validators are defined for Request DTOs (e.g., `LoginRequest`), but MediatR's `ValidationBehaviour` searches for validators matching Command/Query types (e.g., `LoginCommand`). Since no validators exist for Commands/Queries, server-side validation is effectively bypassed.

## Current Architecture

```
Client (Blazor) → Request DTO → Controller → Command → MediatR Pipeline → Handler
                      ↑                           ↑
                Validators exist           No validators found
                (FluentValidationValidator)  (ValidationBehaviour skips)
```

## Affected Files

- `PredictionLeague.API/DependencyInjection.cs` (line 70): Registers validators
- `PredictionLeague.Application/Common/Behaviours/ValidationBehaviour.cs`: Looks for `IValidator<TRequest>` where TRequest is Command/Query
- `PredictionLeague.Validators/*`: All validators are for Request DTOs

## Why It's Not Critical

The impact is reduced because:

1. **Client-side validation works**: `FluentValidationValidator` in Blazor forms validates before submission
2. **Domain model guards**: `Ardalis.GuardClauses` validates in entity constructors
3. **Database constraints**: Column lengths, types, and relationships enforce limits
4. **ASP.NET model binding**: Basic type validation happens automatically

## Exploitation Scenario

1. Attacker bypasses client-side validation (direct API call)
2. Sends malformed data: `{ "firstName": "" }` or `{ "email": "not-an-email" }`
3. Validation skipped at MediatR level
4. Domain guards or database constraints may reject (but with less user-friendly errors)

## Recommended Fixes

### Option A: Add Automatic Validation at ASP.NET Core Level (Recommended)

Install `FluentValidation.AspNetCore`:
```bash
dotnet add package FluentValidation.AspNetCore
```

Update `DependencyInjection.cs`:
```csharp
// Replace:
services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// With:
services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
services.AddFluentValidationAutoValidation();
```

This validates Request DTOs before they reach the controller.

### Option B: Create Validators for Commands/Queries

Create validators that match Command types:

```csharp
// New file: LoginCommandValidator.cs
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Please enter your email address.")
            .EmailAddress().WithMessage("Please enter a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Please enter your password.")
            .MaximumLength(100);
    }
}
```

Update registration to scan both assemblies:
```csharp
services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
services.AddValidatorsFromAssemblyContaining<LoginCommandValidator>();
```

### Option C: Validate in Controller (Least Recommended)

Manually validate in each controller action:
```csharp
[HttpPost("login")]
public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
{
    var validator = new LoginRequestValidator();
    var result = await validator.ValidateAsync(request, ct);
    if (!result.IsValid)
        return BadRequest(result.Errors);

    // ... continue
}
```

## Testing

1. Use a tool like Postman or curl to bypass client
2. Send invalid data directly to API endpoints
3. Verify appropriate error responses (400 Bad Request with validation messages)

## Implementation Priority

This is P2 because:
- Client-side validation catches most issues
- Domain guards provide fallback protection
- No known active exploitation vector

However, defence-in-depth requires server-side validation for:
- API consumers who bypass the Blazor client
- Mobile apps or third-party integrations
- Compliance requirements

## References

- [FluentValidation ASP.NET Integration](https://docs.fluentvalidation.net/en/latest/aspnet.html)
- [OWASP Input Validation](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)

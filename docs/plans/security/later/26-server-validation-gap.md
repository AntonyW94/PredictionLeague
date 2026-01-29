# Deferred: Server-Side Validation Gap

> **DEFERRED**: The recommended fix (FluentValidation.AspNetCore auto-validation) has been deprecated by the FluentValidation team. Alternative fixes require significant refactoring. Current mitigations are adequate.

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | Deferred |
| Severity | P2 - Medium |
| Type | Input Validation Bypass |
| CWE | CWE-20 (Improper Input Validation) |
| OWASP | A03:2021 - Injection (related) |

---

## Description

FluentValidation validators are defined for Request DTOs (e.g., `LoginRequest`), but MediatR's `ValidationBehaviour` searches for validators matching Command/Query types (e.g., `LoginCommand`). Since no validators exist for Commands/Queries, server-side validation at the MediatR level is effectively bypassed.

## Current Architecture

```
Client (Blazor) → Request DTO → Controller → Command → MediatR Pipeline → Handler
                      ↑                           ↑
                Validators exist           No validators found
                (FluentValidationValidator)  (ValidationBehaviour skips)
```

---

## Why It's Deferred

### Option A is Deprecated

The originally recommended fix was to use `FluentValidation.AspNetCore` with `AddFluentValidationAutoValidation()`. However, the FluentValidation team has **deprecated the entire FluentValidation.AspNetCore package** for the following reasons:

1. ASP.NET Core's validation pipeline is not asynchronous, limiting FluentValidation features
2. Support and maintenance burden is too high
3. Auto-validation makes it hard to see where validators are actually used

**Source:** [FluentValidation Deprecation Announcement](https://github.com/FluentValidation/FluentValidation/issues/1965)

### Option B Requires Significant Effort

Creating validators for all Commands/Queries would require:
- Duplicating validation logic (Request DTOs already have validators)
- Creating ~50+ new validator classes
- Ongoing maintenance to keep both sets in sync

### Current Mitigations Are Adequate

The impact is reduced because:

1. **Client-side validation works**: `FluentValidationValidator` in Blazor forms validates before submission
2. **Domain model guards**: `Ardalis.GuardClauses` validates in entity constructors
3. **Database constraints**: Column lengths, types, and relationships enforce limits
4. **ASP.NET model binding**: Basic type validation happens automatically

---

## Exploitation Scenario

1. Attacker bypasses client-side validation (direct API call)
2. Sends malformed data: `{ "firstName": "" }` or `{ "email": "not-an-email" }`
3. Validation skipped at MediatR level
4. Domain guards or database constraints reject with less user-friendly errors

---

## Potential Future Fixes

### Option B: Create Validators for Commands/Queries (High Effort)

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

### Option C: Manual Validation in Controllers (Medium Effort)

Validate Request DTOs manually in each controller action before mapping to Commands:

```csharp
[HttpPost("login")]
public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
{
    var validator = new LoginRequestValidator();
    var result = await validator.ValidateAsync(request, ct);
    if (!result.IsValid)
        return BadRequest(result.Errors);

    // Map to command and continue...
}
```

---

## Decision

**Status:** Accepted risk - deferred indefinitely

**Rationale:**
- Recommended fix is deprecated
- Alternative fixes require significant refactoring
- Current mitigations (client-side validation, domain guards, database constraints) provide adequate protection
- No known active exploitation in production

**Review trigger:**
- If FluentValidation introduces a new recommended approach
- If third-party API consumers are added
- If compliance requirements change

**Last reviewed:** January 29, 2026

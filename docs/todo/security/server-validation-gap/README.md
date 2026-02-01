# Server-Side Validation Gap

## Status

**Deferred** - Accepted risk

## Summary

FluentValidation validators are defined for Request DTOs, but MediatR's ValidationBehaviour searches for validators matching Command/Query types. Since no validators exist for Commands/Queries, server-side validation at the MediatR level is effectively bypassed.

## Priority

**Deferred**

## Severity

**Medium** - P2

## CWE Reference

CWE-20 (Improper Input Validation)

## OWASP Reference

A03:2021 - Injection (related)

## Problem Description

### Current Architecture

```
Client (Blazor) → Request DTO → Controller → Command → MediatR Pipeline → Handler
                      ↑                           ↑
                Validators exist           No validators found
                (FluentValidationValidator)  (ValidationBehaviour skips)
```

### Exploitation Scenario

1. Attacker bypasses client-side validation (direct API call)
2. Sends malformed data: `{ "firstName": "" }` or `{ "email": "not-an-email" }`
3. Validation skipped at MediatR level
4. Domain guards or database constraints reject with less user-friendly errors

## Why It's Deferred

### Option A is Deprecated

The originally recommended fix (`FluentValidation.AspNetCore` with `AddFluentValidationAutoValidation()`) has been **deprecated** by the FluentValidation team because:
- ASP.NET Core's validation pipeline is not asynchronous
- Support and maintenance burden is too high
- Auto-validation makes it hard to see where validators are used

### Option B Requires Significant Effort

Creating validators for all Commands/Queries would require:
- Duplicating validation logic (Request DTOs already have validators)
- Creating ~50+ new validator classes
- Ongoing maintenance to keep both sets in sync

## Current Mitigations

1. **Client-side validation works** - FluentValidationValidator in Blazor forms validates before submission
2. **Domain model guards** - Ardalis.GuardClauses validates in entity constructors
3. **Database constraints** - Column lengths, types, and relationships enforce limits
4. **ASP.NET model binding** - Basic type validation happens automatically

## Potential Future Fixes

### Option B: Create Validators for Commands/Queries (High Effort)

```csharp
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
    }
}
```

### Option C: Manual Validation in Controllers (Medium Effort)

```csharp
[HttpPost("login")]
public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
{
    var validator = new LoginRequestValidator();
    var result = await validator.ValidateAsync(request, ct);
    if (!result.IsValid)
        return BadRequest(result.Errors);
    // Continue...
}
```

## Decision

**Status:** Accepted risk - deferred indefinitely

**Rationale:**
- Recommended fix is deprecated
- Alternative fixes require significant refactoring
- Current mitigations provide adequate protection
- No known active exploitation

**Review trigger:**
- If FluentValidation introduces a new recommended approach
- If third-party API consumers are added
- If compliance requirements change

**Last reviewed:** January 2026

# Task 2: Param Resolvers (Smart Defaults)

**Parent Feature:** [Email Test Tool](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped (PR #103) - `EmailTestDefaultsResolver` provides smart per-template defaults.

## Goal

Pre-fill the test form's parameter inputs with realistic values for the selected user. For most params this is a simple name match against `ApplicationUser` fields. For specific templates (notably `Password Reset`) a per-template resolver generates a real reset token and URL so the link in the test email actually works.

The resolver is a *suggestion layer*. The admin can overwrite any value in the form before sending.

## Architecture

A registry of resolvers keyed by Brevo template name. A request to "give me defaults for template T sent to user U" runs:

1. The generic name-matching resolver (always runs).
2. The specific resolver for template T, if one is registered (overrides any keys it sets).

The result is a `Dictionary<string, string>` of `paramName` -> `defaultValue`.

```csharp
namespace ThePredictions.Application.Services;

public interface ITemplateParamResolver
{
    string TemplateName { get; }

    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        ApplicationUser recipient,
        IReadOnlyList<string> parameterNames,
        string baseUrl,
        CancellationToken cancellationToken);
}
```

A coordinating service composes them:

```csharp
public interface ITemplateDefaultsService
{
    Task<IReadOnlyDictionary<string, string>> GetDefaultsAsync(
        string templateName,
        ApplicationUser dataSourceUser,
        IReadOnlyList<string> parameterNames,
        string baseUrl,
        CancellationToken cancellationToken);
}
```

`GetDefaultsAsync` calls the generic resolver then, if a template-specific resolver is registered, merges its output on top.

## Generic resolver

Maps param names to user fields, case-insensitive. Only writes to keys actually present in `parameterNames` so we never inject keys the template doesn't ask for.

| Param name (any of these) | Source |
|---|---|
| `firstName`, `firstname` | `user.FirstName` |
| `lastName`, `lastname`, `surname` | `user.LastName` |
| `fullName`, `name` | `$"{user.FirstName} {user.LastName}".Trim()` |
| `email`, `emailAddress` | `user.Email` |

Anything not matched is left absent (empty input in the UI).

## Per-template resolvers

### `Password Reset`

Generates a real token using the same logic as
[`RequestPasswordResetCommandHandler.GenerateUrlSafeToken`](../../../../src/ThePredictions.Application/Features/Authentication/Commands/RequestPasswordReset/RequestPasswordResetCommandHandler.cs:115),
persists it via `IPasswordResetTokenRepository.CreateAsync`, and builds the link the same way the live handler does.

```
resetLink = $"{baseUrl}/authentication/reset-password?token={token}"
```

The token is for the data-source user, so clicking the link in the test email would reset *that user's* password. The admin still receives the email at their own address (see Task 3), so they need to be aware of this if they actually click the link.

To extract duplication, move both the token generation and link building into a small helper class shared between the live handler and this resolver. Suggested class:
`ThePredictions.Application.Services.PasswordResetTokenIssuer` with `IssueAsync(user, baseUrl, ct) -> string resetLink`.

The live handler should be refactored to use this helper as part of this task; tests should still pass without modification.

### `Password Reset - Google User`

```
loginLink = $"{baseUrl}/authentication/login"
```

No token needed. Trivial.

## Tests

In `ThePredictions.Application.Tests.Unit`:

- Generic resolver:
  - `Resolver_ShouldFillFirstName_WhenParamMatchesUserField`
  - `Resolver_ShouldFillFullName_WhenParamCalledFullName`
  - `Resolver_ShouldIgnoreParam_WhenNoMatchingUserField`
  - `Resolver_ShouldOnlyReturnRequestedParams_WhenUserHasMore`
- `PasswordResetTokenIssuer`:
  - `Issue_ShouldReturnLinkContainingToken_WhenCalled`
  - `Issue_ShouldPersistTokenForUser_WhenCalled`
  - `Issue_ShouldGenerateUniqueTokens_WhenCalledTwice`
- `PasswordResetParamResolver`:
  - `Resolve_ShouldFillResetLink_WhenTemplatePassword`
  - `Resolve_ShouldFallBackToGenericForFirstName_WhenTemplatePassword`
- Live `RequestPasswordResetCommandHandler` tests should continue to pass after the refactor to use `PasswordResetTokenIssuer`.

## Verification

- [ ] Existing `RequestPasswordResetCommandHandler` tests pass after the refactor.
- [ ] Selecting `Password Reset` in the test page pre-fills `firstName` and `resetLink` with realistic values.
- [ ] Clicking the link in the resulting email lands on the reset-password page and the form accepts a new password.
- [ ] Selecting `Password Reset - Google User` pre-fills `firstName` and `loginLink`.
- [ ] Selecting an unknown template still pre-fills `firstName`, `lastName`, etc. via the generic resolver.

## Open questions

- [ ] Should test-issued tokens be marked / shorter-lived than real ones? Probably not worth the complexity - 1 hour expiry already covers it.

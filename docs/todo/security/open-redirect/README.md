# Open Redirect Vulnerability

## Status

**Not Started** | In Progress | Complete

> **DEFERRED**: This plan involves changes to the Google OAuth login flow (ExternalAuthController). It has been deferred until the login system is ready to be modified.

## Summary

Fix open redirect vulnerability in ExternalAuthController where `returnUrl` and `source` parameters are used without validation.

## Priority

**High** - P1

## Severity

**High** - Attackers can redirect users to malicious sites after authentication

## CWE Reference

[CWE-601: URL Redirection to Untrusted Site ('Open Redirect')](https://cwe.mitre.org/data/definitions/601.html)

## Problem Description

The `ExternalAuthController` accepts `returnUrl` and `source` parameters from user input and uses them directly in `Redirect()` calls without validation. This allows attackers to craft URLs that redirect users to malicious sites after Google OAuth authentication.

### Affected Files

| File | Lines |
|------|-------|
| `PredictionLeague.API/Controllers/ExternalAuthController.cs` | 27, 52, 61, 72-74 |

### Attack Scenario

1. Attacker crafts URL: `https://predictionleague.com/external-auth/google-login?returnUrl=https://evil.com/steal-token&source=/login`
2. Victim clicks the link
3. Victim authenticates with Google (legitimate)
4. After successful auth, victim is redirected to `https://evil.com/steal-token?refreshToken=xxx`
5. Attacker captures the refresh token from their malicious site

## Requirements

- [ ] Add `IsValidLocalUrl` helper method to validate URLs are local relative paths
- [ ] Update `GoogleLogin` to validate returnUrl and source parameters
- [ ] Update `GoogleCallback` with defense-in-depth validation
- [ ] Update `RedirectWithError` to validate URL before redirect
- [ ] Add logging for invalid URL attempts
- [ ] Manual testing with bypass attempts (`//evil.com`, `/\evil.com`, `javascript:`)
- [ ] Code review approved
- [ ] Deploy and verify in production

## Solution

### URL Validation Helper

```csharp
private static bool IsValidLocalUrl(string? url)
{
    if (string.IsNullOrEmpty(url))
        return false;

    // Only allow relative URLs starting with /
    if (!url.StartsWith('/'))
        return false;

    // Block protocol-relative URLs (//evil.com)
    if (url.StartsWith("//"))
        return false;

    // Block URLs with backslash (\/evil.com in some browsers)
    if (url.Contains('\\'))
        return false;

    return true;
}
```

### Updated GoogleLogin

```csharp
var safeReturnUrl = IsValidLocalUrl(returnUrl) ? returnUrl : "/";
var safeSource = IsValidLocalUrl(source) ? source : "/login";
```

## Testing

### Manual Testing (Required)

1. Attempt OAuth flow with `returnUrl=https://evil.com`
2. Verify redirect goes to `/` not external site
3. Test bypass attempts: `//evil.com`, `/\evil.com`, `javascript:alert(1)`

### Future: Unit Tests

```csharp
[Theory]
[InlineData("/dashboard", true)]
[InlineData("/leagues/123", true)]
[InlineData("https://evil.com", false)]
[InlineData("//evil.com", false)]
[InlineData("/\\evil.com", false)]
public void IsValidLocalUrl_ValidatesCorrectly(string url, bool expected)
```

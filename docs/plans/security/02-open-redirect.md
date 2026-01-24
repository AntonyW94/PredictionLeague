# Fix 02: Open Redirect Vulnerability

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P1 - High** - Fix this sprint

## Severity
**High** - Attackers can redirect users to malicious sites after authentication

## CWE Reference
[CWE-601: URL Redirection to Untrusted Site ('Open Redirect')](https://cwe.mitre.org/data/definitions/601.html)

---

## Problem Description

The `ExternalAuthController` accepts `returnUrl` and `source` parameters from user input and uses them directly in `Redirect()` calls without validation. This allows attackers to craft URLs that redirect users to malicious sites after Google OAuth authentication.

### Affected Files

| File | Lines |
|------|-------|
| `PredictionLeague.API/Controllers/ExternalAuthController.cs` | 27, 52, 61, 72-74 |

### Current Vulnerable Code

**GoogleLogin (Line 27)**
```csharp
[HttpGet("google-login")]
[AllowAnonymous]
public IActionResult GoogleLogin([FromQuery] string returnUrl, [FromQuery] string source)
{
    var properties = new AuthenticationProperties
    {
        RedirectUri = callbackUrl,
        Items =
        {
            { "returnUrl", returnUrl },  // Unvalidated user input
            { "source", source }         // Unvalidated user input
        }
    };
    return Challenge(properties, GoogleDefaults.AuthenticationScheme);
}
```

**GoogleCallback (Line 61)**
```csharp
return Redirect($"{returnUrl}?refreshToken={encodedToken}&source={source}");
// returnUrl could be https://evil.com
```

**RedirectWithError (Line 72-74)**
```csharp
private IActionResult RedirectWithError(string returnUrl, string error)
{
    return Redirect($"{returnUrl}?error={Uri.EscapeDataString(error)}");
}
```

### Attack Scenario

1. Attacker crafts URL: `https://predictionleague.com/external-auth/google-login?returnUrl=https://evil.com/steal-token&source=/login`
2. Victim clicks the link
3. Victim authenticates with Google (legitimate)
4. After successful auth, victim is redirected to `https://evil.com/steal-token?refreshToken=xxx`
5. Attacker captures the refresh token from their malicious site

---

## Solution

### Step 1: Add URL Validation Helper

Add a private method to validate that URLs are local (relative paths only).

**File**: `PredictionLeague.API/Controllers/ExternalAuthController.cs`

```csharp
/// <summary>
/// Validates that a URL is a local relative path to prevent open redirect attacks.
/// </summary>
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

### Step 2: Update GoogleLogin

Validate URLs before storing them in authentication properties.

```csharp
[HttpGet("google-login")]
[AllowAnonymous]
public IActionResult GoogleLogin([FromQuery] string returnUrl, [FromQuery] string source)
{
    _logger.LogInformation("Called google-login");

    // Validate and sanitize redirect URLs
    var safeReturnUrl = IsValidLocalUrl(returnUrl) ? returnUrl : "/";
    var safeSource = IsValidLocalUrl(source) ? source : "/login";

    var callbackUrl = Url.Action("GoogleCallback");
    var properties = new AuthenticationProperties
    {
        RedirectUri = callbackUrl,
        Items =
        {
            { "returnUrl", safeReturnUrl },
            { "source", safeSource }
        }
    };

    return Challenge(properties, GoogleDefaults.AuthenticationScheme);
}
```

### Step 3: Update GoogleCallback

Add validation when extracting URLs from properties (defense in depth).

```csharp
[HttpGet("signin-google")]
[AllowAnonymous]
public async Task<IActionResult> GoogleCallback(CancellationToken cancellationToken)
{
    // ... authentication logic ...

    var returnUrl = authenticateResult.Properties?.Items["returnUrl"] ?? "/";
    var source = authenticateResult.Properties?.Items["source"] ?? "/login";

    // Defense in depth - validate again before redirect
    if (!IsValidLocalUrl(returnUrl))
    {
        _logger.LogWarning("Invalid returnUrl detected in callback: {ReturnUrl}", returnUrl);
        returnUrl = "/";
    }

    if (!IsValidLocalUrl(source))
    {
        _logger.LogWarning("Invalid source detected in callback: {Source}", source);
        source = "/login";
    }

    // ... rest of method ...
}
```

### Step 4: Update RedirectWithError

Validate before redirecting on error.

```csharp
private IActionResult RedirectWithError(string returnUrl, string error)
{
    var safeReturnUrl = IsValidLocalUrl(returnUrl) ? returnUrl : "/login";
    return Redirect($"{safeReturnUrl}?error={Uri.EscapeDataString(error)}");
}
```

---

## Testing Requirements

> **Note**: Automated testing is deferred until test project infrastructure is in place.
> Test code is preserved below for future implementation.

### Manual Testing (Required for this implementation)

1. Attempt OAuth flow with `returnUrl=https://evil.com`
2. Verify redirect goes to `/` not external site
3. Test with various bypass attempts: `//evil.com`, `/\evil.com`, `javascript:alert(1)`

### Future: Unit Tests

<details>
<summary>Click to expand test code for future implementation</summary>

```csharp
[Theory]
[InlineData("/dashboard", true)]
[InlineData("/leagues/123", true)]
[InlineData("/", true)]
[InlineData("https://evil.com", false)]
[InlineData("//evil.com", false)]
[InlineData("http://evil.com", false)]
[InlineData("/\\evil.com", false)]
[InlineData("", false)]
[InlineData(null, false)]
public void IsValidLocalUrl_ValidatesCorrectly(string url, bool expected)
{
    var result = ExternalAuthController.IsValidLocalUrl(url);
    Assert.Equal(expected, result);
}
```

</details>

### Future: Integration Tests

1. Call `/external-auth/google-login?returnUrl=https://evil.com` - should use `/` as returnUrl
2. Call `/external-auth/google-login?returnUrl=//evil.com` - should use `/` as returnUrl
3. Call `/external-auth/google-login?returnUrl=/dashboard` - should preserve `/dashboard`

---

## Security Considerations

- Log invalid URL attempts for monitoring
- Consider rate limiting the OAuth endpoints
- Review other endpoints that accept redirect URLs

---

## Checklist

- [ ] Add `IsValidLocalUrl` helper method
- [ ] Update `GoogleLogin` to validate input
- [ ] Update `GoogleCallback` with defense-in-depth validation
- [ ] Update `RedirectWithError` to validate URL
- [ ] Add logging for invalid URL attempts
- [ ] Manual testing with bypass attempts
- [ ] Code review approved
- [ ] Deployed to production
- [ ] Verified in production

### Future (when test projects added)
- [ ] Write unit tests for URL validation
- [ ] Write integration tests

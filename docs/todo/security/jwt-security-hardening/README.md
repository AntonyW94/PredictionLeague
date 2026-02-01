# JWT Security Hardening

## Status

**Not Started** | In Progress | Complete

> **DEFERRED**: This plan involves changes to the login system and cookies. It has been deferred until the login system is ready to be modified.

## Summary

Harden JWT configuration including SameSite cookie settings, clock skew, and algorithm validation.

## Priority

**Critical** - P0

## Severity

**Critical** - Authentication / Token Security

## CWE References

- CWE-384: Session Fixation
- CWE-327: Broken Crypto

## OWASP References

- A02:2021 Cryptographic Failures
- A07:2021 Identification Failures

## Vulnerabilities Addressed

### 1. SameSite=None on Refresh Token Cookies (CRITICAL)

**File:** `PredictionLeague.API/Controllers/AuthControllerBase.cs:24`

Current code uses `SameSite = SameSiteMode.None` which allows cross-site request forgery on token refresh endpoint.

### 2. No ClockSkew Configured (MEDIUM)

**File:** `PredictionLeague.API/DependencyInjection.cs:46-55`

Using default 5-minute ClockSkew without explicit configuration.

### 3. No ValidateAlgorithm Whitelist (MEDIUM)

**File:** `PredictionLeague.API/DependencyInjection.cs:46-55`

No explicit algorithm whitelist; could allow algorithm confusion attacks.

## Requirements

- [ ] Change SameSite from None to Strict on refresh token cookies
- [ ] Restrict cookie path from `/` to `/api/auth`
- [ ] Add explicit ClockSkew configuration (30 seconds)
- [ ] Add ValidAlgorithms whitelist for HmacSha256
- [ ] Verify token generation uses matching algorithm
- [ ] Test SameSite=Strict doesn't break Blazor WASM flow
- [ ] Browser DevTools verification of cookie settings
- [ ] Deploy and verify in production

## Solution

### Fix SameSite Cookie Setting

```csharp
var cookieOptions = new CookieOptions
{
    HttpOnly = true,
    Expires = DateTime.UtcNow.AddDays(expiryDays),
    Secure = true,
    SameSite = SameSiteMode.Strict,  // Changed from None
    Path = "/api/auth",               // Restricted path
    Domain = ".thepredictions.co.uk"
};
```

### Add ClockSkew and Algorithm Validation

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    // ... existing settings ...
    ClockSkew = TimeSpan.FromSeconds(30),
    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
};
```

## Rollback Plan

If SameSite=Strict causes authentication issues:
```csharp
SameSite = SameSiteMode.Lax,  // Fallback
```

If ClockSkew causes legitimate token rejections:
```csharp
ClockSkew = TimeSpan.FromSeconds(60),
```

## Testing

### Browser DevTools Verification

1. Open DevTools > Application > Cookies
2. Find "refreshToken" cookie
3. Verify: HttpOnly=true, Secure=true, SameSite=Strict, Path=/api/auth

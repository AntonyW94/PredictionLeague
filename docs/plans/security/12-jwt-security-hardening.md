# Fix Plan: JWT Security Hardening

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | P0 - Critical |
| Severity | Critical |
| Type | Authentication / Token Security |
| CWE | CWE-384: Session Fixation, CWE-327: Broken Crypto |
| OWASP | A02:2021 Cryptographic Failures, A07:2021 Identification Failures |

---

## Vulnerabilities Addressed

### 1. SameSite=None on Refresh Token Cookies (CRITICAL)

**File:** `PredictionLeague.API/Controllers/AuthControllerBase.cs:24`

**Current Code:**
```csharp
var cookieOptions = new CookieOptions
{
    HttpOnly = true,
    Expires = DateTime.UtcNow.AddDays(expiryDays),
    Secure = true,
    SameSite = SameSiteMode.None,  // VULNERABLE
    Path = "/",
    Domain = ".thepredictions.co.uk"
};
```

**Impact:** Cross-site request forgery possible on token refresh endpoint.

---

### 2. No ClockSkew Configured (MEDIUM)

**File:** `PredictionLeague.API/DependencyInjection.cs:46-55`

**Issue:** Using default 5-minute ClockSkew without explicit configuration.

---

### 3. No ValidateAlgorithm Whitelist (MEDIUM)

**File:** `PredictionLeague.API/DependencyInjection.cs:46-55`

**Issue:** No explicit algorithm whitelist; could allow algorithm confusion attacks.

---

## Fix Implementation

### Step 1: Fix SameSite Cookie Setting

**File:** `PredictionLeague.API/Controllers/AuthControllerBase.cs`

```csharp
protected void SetTokenCookie(string refreshToken, int expiryDays = 7)
{
    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        Expires = DateTime.UtcNow.AddDays(expiryDays),
        Secure = true,
        SameSite = SameSiteMode.Strict,  // FIXED: Changed from None to Strict
        Path = "/api/auth",              // IMPROVED: Restricted path
        Domain = ".thepredictions.co.uk"
    };

    Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
}

protected void ClearTokenCookie()
{
    Response.Cookies.Delete("refreshToken", new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Domain = ".thepredictions.co.uk"
    });
}
```

**Note:** If SameSite=Strict causes issues with the Blazor WASM flow (which is same-origin), use SameSite=Lax as fallback.

---

### Step 2: Add ClockSkew and ValidateAlgorithm Configuration

**File:** `PredictionLeague.API/DependencyInjection.cs`

**Current:**
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = jwtSettings.Issuer,
    ValidAudience = jwtSettings.Audience,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
};
```

**Fixed:**
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = jwtSettings.Issuer,
    ValidAudience = jwtSettings.Audience,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),

    // NEW: Explicit clock skew configuration
    ClockSkew = TimeSpan.FromSeconds(30),  // Reduced from default 5 minutes

    // NEW: Algorithm whitelist to prevent algorithm confusion attacks
    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
};
```

---

### Step 3: Ensure Token Generation Uses Correct Algorithm

**File:** `PredictionLeague.Infrastructure/Services/AuthenticationTokenService.cs`

**Verify this line exists (should already be correct):**
```csharp
var creds = new SigningCredentials(
    new SymmetricSecurityKey(key),
    SecurityAlgorithms.HmacSha256  // Must match ValidAlgorithms whitelist
);
```

---

## Configuration Changes

### Update JwtSettings if needed

**File:** `appsettings.json` (optional enhancement)

```json
"JwtSettings": {
    "Secret": "${Jwt-Secret}",
    "Issuer": "https://www.thepredictions.co.uk",
    "Audience": "https://www.thepredictions.co.uk",
    "ExpiryMinutes": 30,           // Reduced from 60 for shorter exposure window
    "RefreshTokenExpiryDays": 3,   // Reduced from 7 for tighter security
    "ClockSkewSeconds": 30         // New configurable setting
}
```

---

## Testing

### Manual Test Steps

1. **SameSite Cookie Test:**
   - Log in and verify refresh token cookie has SameSite=Strict
   - Try accessing refresh endpoint from external site (should fail)
   - Verify normal refresh flow still works from application

2. **ClockSkew Test:**
   - Create token with current time
   - Wait 31 seconds after token expiry
   - Verify token is rejected (within 30-second skew, accepted; after, rejected)

3. **Algorithm Test:**
   - Attempt to forge token with RS256 algorithm (should fail)
   - Verify HS256 tokens are accepted

### Browser DevTools Verification
```
1. Open DevTools > Application > Cookies
2. Find "refreshToken" cookie
3. Verify:
   - HttpOnly: true
   - Secure: true
   - SameSite: Strict
   - Path: /api/auth
```

---

## Rollback Plan

If SameSite=Strict causes authentication issues:

```csharp
// Fallback to Lax (still provides CSRF protection for top-level navigations)
SameSite = SameSiteMode.Lax,
```

If ClockSkew causes legitimate token rejections:
```csharp
// Increase to 60 seconds if needed
ClockSkew = TimeSpan.FromSeconds(60),
```

---

## Notes

- SameSite=Strict is the most secure option but may affect some cross-origin flows
- The 30-second ClockSkew is appropriate for well-synchronized systems
- Algorithm whitelist prevents "none" algorithm attacks and RS256/HS256 confusion

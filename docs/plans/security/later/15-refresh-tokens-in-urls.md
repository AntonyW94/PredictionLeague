# Deferred: Refresh Tokens in URL Parameters

> **DEFERRED**: This is required for mobile browser OAuth compatibility. Changing it would break Google login on mobile devices.

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | Deferred |
| Severity | Medium |
| Type | Token Exposure |
| CWE | CWE-598 (Information Exposure Through Query Strings) |
| OWASP | A04:2021 - Insecure Design |

---

## Description

During Google OAuth callback, the refresh token is passed in the URL query string to the Blazor client.

**File:** `PredictionLeague.API/Controllers/ExternalAuthController.cs`

**Current Flow:**
```
1. User clicks "Login with Google"
2. Redirected to Google OAuth
3. Google redirects back to API callback
4. API generates tokens
5. API redirects to Blazor with tokens in URL: /auth/callback?accessToken=xxx&refreshToken=yyy
6. Blazor reads tokens from URL and stores them
```

**Security Concern:**
- Tokens appear in browser history
- Tokens may be logged by proxies/firewalls
- Tokens visible in referrer headers if user navigates away

---

## Why It's Deferred

Mobile browsers (especially Safari on iOS) have restrictions on:
- Setting cookies during cross-origin redirects
- Accessing cookies set by the API domain from the client domain

The current URL-based approach ensures reliable token delivery across all browsers and devices.

---

## Current Mitigations

1. **HTTPS enforced** - Tokens encrypted in transit
2. **Short-lived access tokens** - 15 minute expiry limits exposure window
3. **Refresh token rotation** - Each use generates new token, old one invalidated
4. **Single-use tokens** - Tokens consumed immediately on callback page
5. **History replacement** - Blazor replaces URL to remove tokens from history (if implemented)

---

## Potential Future Fix

### Option A: Use HTTP-Only Cookies (Preferred)

Would require:
1. Same domain for API and client (currently both on thepredictions.co.uk)
2. Testing across all target browsers
3. Handling cookie blocking/privacy modes

```csharp
// In ExternalAuthController callback
Response.Cookies.Append("accessToken", accessToken, new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Expires = DateTime.UtcNow.AddMinutes(15)
});

// Redirect without tokens in URL
return Redirect($"{clientUrl}/auth/callback");
```

### Option B: Use POST with Form Auto-Submit

```csharp
// Return HTML page that auto-submits form to client
return Content($@"
<html>
<body onload='document.forms[0].submit()'>
<form method='POST' action='{clientUrl}/auth/callback'>
    <input type='hidden' name='accessToken' value='{accessToken}' />
    <input type='hidden' name='refreshToken' value='{refreshToken}' />
</form>
</body>
</html>", "text/html");
```

### Option C: Encrypted/Signed Token Parameter

Use a short-lived, encrypted wrapper token that the client exchanges for real tokens via API call.

---

## Testing Required Before Fix

1. Test on iOS Safari (strictest cookie handling)
2. Test on Android Chrome
3. Test on desktop browsers with various privacy settings
4. Test with ad blockers enabled
5. Test in private/incognito mode

---

## Decision

**Status:** Accepted risk for mobile compatibility

**Review trigger:** If mobile browser cookie handling improves, or if we move to a native mobile app with different auth flows.

**Last reviewed:** January 27, 2026

# Fix Plan: Configuration Hardening

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | P2 - Medium |
| Severity | Medium |
| Type | Security Misconfiguration |
| CWE | CWE-16: Configuration |
| OWASP | A05:2021 Security Misconfiguration |

---

## Vulnerabilities Addressed

### 1. AllowedHosts Wildcard (MEDIUM)

**File:** `PredictionLeague.Web/appsettings.json`

**Current:**
```json
"AllowedHosts": "*"
```

**Issue:** Accepts requests with any Host header, enabling host header injection attacks.

---

### 2. HSTS Header Missing from API (MEDIUM)

**File:** `PredictionLeague.API/Middleware/SecurityHeadersMiddleware.cs`

**Issue:** SecurityHeadersMiddleware does not include HSTS header. Only the Web project uses `UseHsts()`.

---

## Fix Implementation

### Fix 1: Restrict AllowedHosts

**File:** `PredictionLeague.Web/appsettings.json`

**Change from:**
```json
"AllowedHosts": "*"
```

**To:**
```json
"AllowedHosts": "www.thepredictions.co.uk;thepredictions.co.uk"
```

**File:** `PredictionLeague.Web/appsettings.Development.json`

```json
"AllowedHosts": "localhost"
```

---

### Fix 2: Add HSTS Header to SecurityHeadersMiddleware

**File:** `PredictionLeague.API/Middleware/SecurityHeadersMiddleware.cs`

**Add HSTS header:**

```csharp
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Existing headers
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // NEW: Add HSTS header
        // Only add for HTTPS requests to avoid issues during development
        if (context.Request.IsHttps)
        {
            context.Response.Headers.Append(
                "Strict-Transport-Security",
                "max-age=31536000; includeSubDomains");
        }

        // Permissions Policy
        context.Response.Headers.Append("Permissions-Policy",
            "accelerometer=(), camera=(), geolocation=(), gyroscope=(), " +
            "magnetometer=(), microphone=(), payment=(), usb=()");

        // Content Security Policy (existing)
        var csp = "default-src 'self'; " +
                  "script-src 'self' 'wasm-unsafe-eval'; " +
                  "style-src 'self' 'unsafe-inline'; " +
                  "img-src 'self' data: https:; " +
                  "font-src 'self' data:; " +
                  "connect-src 'self' https://accounts.google.com; " +
                  "frame-ancestors 'none'; " +
                  "form-action 'self'; " +
                  "base-uri 'self'; " +
                  "upgrade-insecure-requests";

        context.Response.Headers.Append("Content-Security-Policy", csp);

        await _next(context);
    }
}
```

---

### Fix 3: Configure HSTS in Web Project (Optional Enhancement)

**File:** `PredictionLeague.Web/Program.cs`

The existing `app.UseHsts()` uses default settings. For explicit configuration:

```csharp
// In ConfigureServices
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
    options.ExcludedHosts.Add("localhost");
});

// In Configure (existing)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
```

---

## Additional Configuration Recommendations

### Cookie Policy Enhancement

**File:** `PredictionLeague.Web/Program.cs`

```csharp
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
});
```

### Data Protection Keys (Production)

If using Azure, consider Key Vault for data protection:

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(connectionString, containerName, blobName)
    .ProtectKeysWithAzureKeyVault(new Uri(keyIdentifier), new DefaultAzureCredential());
```

---

## Testing

### HSTS Verification

1. Access API endpoint via browser
2. Open DevTools > Network > Response Headers
3. Verify `Strict-Transport-Security: max-age=31536000; includeSubDomains` present

### AllowedHosts Verification

1. Use curl with custom Host header:
   ```bash
   curl -H "Host: malicious.com" https://www.thepredictions.co.uk/api/health
   ```
2. Should return 400 Bad Request (after fix)
3. With legitimate Host header, should return 200 OK

### Online Tools

- https://securityheaders.com - Verify all security headers
- https://www.ssllabs.com/ssltest/ - Verify SSL/TLS configuration
- https://hstspreload.org - Check HSTS preload eligibility

---

## Deployment Checklist

- [ ] Update AllowedHosts in production appsettings
- [ ] Deploy SecurityHeadersMiddleware changes
- [ ] Verify HSTS header present in API responses
- [ ] Test with curl and custom Host headers
- [ ] Run securityheaders.com scan
- [ ] Consider submitting to HSTS preload list

---

## Rollback Plan

If AllowedHosts causes legitimate traffic issues:
```json
"AllowedHosts": "www.thepredictions.co.uk;thepredictions.co.uk;*.thepredictions.co.uk"
```

If HSTS causes issues (should not, but just in case):
- Remove the HSTS header from SecurityHeadersMiddleware
- Note: Browsers cache HSTS, so users may need to clear site data

---

## Notes

- HSTS preload requires consistent HTTPS deployment across all subdomains
- max-age of 1 year (31536000 seconds) is industry standard
- AllowedHosts works at the Kestrel/host level, before middleware
- Consider setting up CSP reporting endpoint for violation monitoring

# Fix 08: Security Headers

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P2 - Medium** - Fix soon

## Severity
**Medium** - Defense in depth against various attacks

## CWE References
- [CWE-1021: Improper Restriction of Rendered UI Layers or Frames](https://cwe.mitre.org/data/definitions/1021.html) (Clickjacking)
- [CWE-693: Protection Mechanism Failure](https://cwe.mitre.org/data/definitions/693.html)

---

## Problem Description

The application does not set critical security headers that protect against common web vulnerabilities:

| Header | Missing | Protection |
|--------|---------|------------|
| `X-Content-Type-Options` | Yes | MIME sniffing attacks |
| `X-Frame-Options` | Yes | Clickjacking |
| `Content-Security-Policy` | Yes | XSS, injection attacks |
| `X-XSS-Protection` | Yes | Legacy XSS protection |
| `Referrer-Policy` | Yes | Information leakage |
| `Permissions-Policy` | Yes | Feature restrictions |
| `Strict-Transport-Security` | Partial | HTTPS enforcement |

---

## Solution

### Option A: Custom Middleware (Recommended for Control)

**Step 1: Create Security Headers Middleware**

**File**: `PredictionLeague.API/Middleware/SecurityHeadersMiddleware.cs`

```csharp
namespace PredictionLeague.API.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent MIME type sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Prevent clickjacking
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // Legacy XSS protection (for older browsers)
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // Control referrer information
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Restrict browser features
        context.Response.Headers.Append("Permissions-Policy",
            "accelerometer=(), " +
            "camera=(), " +
            "geolocation=(), " +
            "gyroscope=(), " +
            "magnetometer=(), " +
            "microphone=(), " +
            "payment=(), " +
            "usb=()");

        // Content Security Policy
        // Note: Adjust based on your actual requirements
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'wasm-unsafe-eval'; " +  // Blazor WASM needs wasm-unsafe-eval
            "style-src 'self' 'unsafe-inline'; " +       // Blazor may need inline styles
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self' https://accounts.google.com; " +  // Google OAuth
            "frame-ancestors 'none'; " +
            "form-action 'self'; " +
            "base-uri 'self'; " +
            "upgrade-insecure-requests;");

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
```

**Step 2: Register Middleware**

**File**: `PredictionLeague.API/Program.cs`

```csharp
using PredictionLeague.API.Middleware;

var app = builder.Build();

// Add security headers early in the pipeline
app.UseSecurityHeaders();

// Existing middleware...
app.UseHttpsRedirection();
app.UseHsts();
// ...
```

---

### Option B: NWebsec Package

For more comprehensive security header management.

**Step 1: Install Package**

```bash
dotnet add PredictionLeague.API package NWebsec.AspNetCore.Middleware
```

**Step 2: Configure in Program.cs**

```csharp
using NWebsec.Core.Common.HttpHeaders.Configuration;

var app = builder.Build();

app.UseXContentTypeOptions();
app.UseXfo(options => options.Deny());
app.UseXXssProtection(options => options.EnabledWithBlockMode());
app.UseReferrerPolicy(options => options.StrictOriginWhenCrossOrigin());

app.UseCsp(options => options
    .DefaultSources(s => s.Self())
    .ScriptSources(s => s.Self().CustomSources("'wasm-unsafe-eval'"))
    .StyleSources(s => s.Self().UnsafeInline())
    .ImageSources(s => s.Self().CustomSources("data:", "https:"))
    .FontSources(s => s.Self().CustomSources("data:"))
    .ConnectSources(s => s.Self().CustomSources("https://accounts.google.com"))
    .FrameAncestors(s => s.None())
    .FormActions(s => s.Self())
    .BaseUris(s => s.Self())
    .UpgradeInsecureRequests()
);
```

---

## Header Details

### X-Content-Type-Options

```
X-Content-Type-Options: nosniff
```

Prevents browsers from MIME-sniffing responses away from the declared content-type. Stops attacks where malicious content is disguised as a safe file type.

### X-Frame-Options

```
X-Frame-Options: DENY
```

Prevents the page from being embedded in iframes on other sites, protecting against clickjacking attacks.

### Content-Security-Policy

```
Content-Security-Policy: default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; ...
```

Restricts which resources can be loaded. Key directives:

| Directive | Value | Purpose |
|-----------|-------|---------|
| `default-src` | 'self' | Default to same-origin only |
| `script-src` | 'self' 'wasm-unsafe-eval' | Allow scripts from same origin + Blazor WASM |
| `style-src` | 'self' 'unsafe-inline' | Allow styles (Blazor may need inline) |
| `img-src` | 'self' data: https: | Allow images from self, data URIs, and HTTPS |
| `connect-src` | 'self' https://accounts.google.com | Allow API calls to self and Google OAuth |
| `frame-ancestors` | 'none' | Prevent embedding (like X-Frame-Options) |

### Referrer-Policy

```
Referrer-Policy: strict-origin-when-cross-origin
```

Controls how much referrer information is sent:
- Same-origin requests: Full URL
- Cross-origin HTTPS→HTTPS: Origin only
- HTTPS→HTTP: No referrer

### Permissions-Policy

```
Permissions-Policy: camera=(), microphone=(), geolocation=()
```

Disables browser features that the application doesn't need, reducing attack surface.

---

## Blazor WebAssembly Considerations

Blazor WASM requires specific CSP adjustments:

1. **`'wasm-unsafe-eval'`** - Required for WebAssembly execution
2. **`'unsafe-inline'`** for styles - Blazor may inject inline styles
3. **`data:`** for fonts/images - Blazor may use data URIs

Test thoroughly after enabling CSP to ensure Blazor functionality isn't broken.

---

## Testing Requirements

> **Note**: Automated testing is deferred until test project infrastructure is in place.
> Test code is preserved below for future implementation.

### Manual Testing (Required for this implementation)

1. Deploy changes to test environment
2. Visit https://securityheaders.com and scan your site
3. Aim for grade A or A+
4. Test all Blazor functionality still works with CSP

### Browser DevTools Verification

1. Open browser DevTools (F12)
2. Go to Network tab
3. Make a request
4. Check Response Headers
5. Verify all security headers are present

### Future: Automated Tests

<details>
<summary>Click to expand test code for future implementation</summary>

```csharp
[Fact]
public async Task SecurityHeaders_ArePresent()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/");

    Assert.True(response.Headers.Contains("X-Content-Type-Options"));
    Assert.True(response.Headers.Contains("X-Frame-Options"));
    Assert.True(response.Headers.Contains("Content-Security-Policy"));
    Assert.True(response.Headers.Contains("Referrer-Policy"));
}

[Fact]
public async Task XFrameOptions_IsDeny()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/");

    var header = response.Headers.GetValues("X-Frame-Options").First();
    Assert.Equal("DENY", header);
}
```

</details>

---

## Expected Security Headers Report

After implementation, securityheaders.com should show:

| Header | Expected Grade |
|--------|---------------|
| X-Content-Type-Options | ✅ |
| X-Frame-Options | ✅ |
| Content-Security-Policy | ✅ |
| Referrer-Policy | ✅ |
| Permissions-Policy | ✅ |
| Strict-Transport-Security | ✅ (already configured) |

---

## Checklist

- [ ] Create `SecurityHeadersMiddleware` class
- [ ] Register middleware in `Program.cs`
- [ ] Configure Content-Security-Policy for Blazor
- [ ] Test Blazor WASM functionality with CSP
- [ ] Scan with securityheaders.com
- [ ] Fix any CSP violations in browser console
- [ ] Code review approved
- [ ] Deployed to production
- [ ] Re-scan production with securityheaders.com

### Future (when test projects added)
- [ ] Write automated tests for header presence

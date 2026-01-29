# P0: Rate Limiting Middleware Not Enabled

## Summary

**Severity:** P0 - Critical
**Type:** Configuration Error
**CWE:** CWE-307 (Improper Restriction of Excessive Authentication Attempts)
**OWASP:** A07:2021 - Identification and Authentication Failures

## Description

Rate limiting is configured in `DependencyInjection.cs` with well-defined policies, but the middleware is never added to the request pipeline in `Program.cs`. The `[EnableRateLimiting("auth")]` attributes on controllers have no effect.

## Affected Files

- `PredictionLeague.Web/PredictionLeague.Web/Program.cs` (missing `app.UseRateLimiter()`)
- `PredictionLeague.API/DependencyInjection.cs` (rate limiting configured here)

## Current Configuration (Unused)

```csharp
// DependencyInjection.cs lines 88-142
services.AddRateLimiter(options =>
{
    // Global: 100 requests/min per IP
    // Auth policy: 10 requests/5 min per IP
    // API policy: 60 requests/min per IP
});
```

## Exploitation Scenario

1. Attacker targets `/api/auth/login` endpoint
2. Unlimited login attempts possible (no rate limiting enforced)
3. Credential stuffing attack with thousands of password attempts
4. Account enumeration via timing differences in responses

## Impact

- Brute-force password attacks
- Credential stuffing attacks
- Account enumeration
- Resource exhaustion/DoS

## Recommended Fix

Add `app.UseRateLimiter()` to the middleware pipeline in `Program.cs`:

```csharp
// PredictionLeague.Web/PredictionLeague.Web/Program.cs
// Add after app.UseRouting() and before app.UseAuthentication()

app.UseRouting();
app.UseRateLimiter();  // ADD THIS LINE
app.UseAuthentication();
app.UseAuthorization();
```

## Testing

1. Make 11 login attempts within 5 minutes
2. Verify 429 Too Many Requests response after 10th attempt
3. Verify `Retry-After` header is present
4. Test global limit (101 requests within 1 minute)

## References

- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [OWASP Rate Limiting](https://cheatsheetseries.owasp.org/cheatsheets/Denial_of_Service_Cheat_Sheet.html)

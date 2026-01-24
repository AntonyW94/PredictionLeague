# Fix 03: API Key Timing Attack

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P1 - High** - Fix this sprint

## Severity
**High** - Attackers can potentially determine API key characters through timing analysis

## CWE Reference
[CWE-208: Observable Timing Discrepancy](https://cwe.mitre.org/data/definitions/208.html)

---

## Problem Description

The `ApiKeyAuthoriseAttribute` uses `string.Equals()` to compare API keys. This method performs early termination when it finds a mismatch, meaning comparisons that fail on the first character are faster than those that fail on the last character. Attackers can exploit this timing difference to determine the correct API key one character at a time.

### Affected Files

| File | Line |
|------|------|
| `PredictionLeague.API/Filters/ApiKeyAuthoriseAttribute.cs` | 22 |

### Current Vulnerable Code

```csharp
public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
{
    var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
    var expectedApiKey = configuration["FootballApi:SchedulerApiKey"];

    if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var potentialApiKey))
    {
        context.Result = new UnauthorizedResult();
        return;
    }

    // VULNERABLE: string.Equals uses early termination
    if (string.IsNullOrEmpty(expectedApiKey) || !expectedApiKey.Equals(potentialApiKey))
    {
        context.Result = new UnauthorizedResult();
        return;
    }

    await next();
}
```

### Attack Scenario

1. Attacker makes many requests with API keys starting with 'a', 'b', 'c', etc.
2. Attacker measures response times for each request
3. One character produces slightly longer response time (matched first character)
4. Attacker repeats for second character, third character, etc.
5. Eventually attacker determines the full API key

Note: This attack requires many requests and precise timing measurements, but is feasible especially in low-latency environments.

---

## Solution

Use `CryptographicOperations.FixedTimeEquals()` which always takes the same amount of time regardless of where the mismatch occurs.

### Updated Code

**File**: `PredictionLeague.API/Filters/ApiKeyAuthoriseAttribute.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PredictionLeague.API.Filters;

public class ApiKeyAuthoriseAttribute : ActionFilterAttribute, IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>();
        var expectedApiKey = configuration["FootballApi:SchedulerApiKey"];

        // Check if header is present
        if (!context.HttpContext.Request.Headers.TryGetValue(
            ApiKeyHeaderName, out var potentialApiKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check if expected key is configured
        if (string.IsNullOrEmpty(expectedApiKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Use constant-time comparison to prevent timing attacks
        var potentialKeyString = potentialApiKey.ToString();
        if (!ConstantTimeEquals(expectedApiKey, potentialKeyString))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }

    /// <summary>
    /// Compares two strings in constant time to prevent timing attacks.
    /// </summary>
    private static bool ConstantTimeEquals(string expected, string actual)
    {
        // Convert to bytes for comparison
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        // If lengths differ, still perform comparison to maintain constant time
        // but use expected length to avoid information leakage about key length
        if (expectedBytes.Length != actualBytes.Length)
        {
            // Compare expected against itself to maintain timing consistency
            CryptographicOperations.FixedTimeEquals(expectedBytes, expectedBytes);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
```

---

## Alternative: Use ASP.NET Core's Built-in API Key Authentication

For a more robust solution, consider using a proper authentication handler:

```csharp
// Program.cs
builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        "ApiKey", options => { });

// ApiKeyAuthenticationHandler.cs
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API Key"));
        }

        var expectedKey = Configuration["FootballApi:SchedulerApiKey"];
        var providedKey = apiKeyHeader.ToString();

        if (!ConstantTimeEquals(expectedKey, providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "Scheduler") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

---

## Testing Requirements

> **Note**: Automated testing is deferred until test project infrastructure is in place.
> Test code is preserved below for future implementation.

### Manual Testing (Required for this implementation)

1. Test with valid API key - should succeed
2. Test with invalid API key - should return 401
3. Test with missing API key header - should return 401

### Future: Unit Tests

<details>
<summary>Click to expand test code for future implementation</summary>

```csharp
[Fact]
public void ConstantTimeEquals_IdenticalStrings_ReturnsTrue()
{
    Assert.True(ApiKeyAuthoriseAttribute.ConstantTimeEquals("abc123", "abc123"));
}

[Fact]
public void ConstantTimeEquals_DifferentStrings_ReturnsFalse()
{
    Assert.False(ApiKeyAuthoriseAttribute.ConstantTimeEquals("abc123", "xyz789"));
}

[Fact]
public void ConstantTimeEquals_DifferentLengths_ReturnsFalse()
{
    Assert.False(ApiKeyAuthoriseAttribute.ConstantTimeEquals("abc", "abcd"));
}

[Fact]
public void ConstantTimeEquals_EmptyStrings_ReturnsTrue()
{
    Assert.True(ApiKeyAuthoriseAttribute.ConstantTimeEquals("", ""));
}
```

</details>

### Future: Timing Analysis Test (Optional)

<details>
<summary>Click to expand timing test code</summary>

```csharp
[Fact]
public void ConstantTimeEquals_TimingIsConsistent()
{
    var expected = "correct-api-key-12345";
    var iterations = 10000;

    // Time comparison that differs on first character
    var earlyMismatch = "xorrect-api-key-12345";
    var earlyTimes = MeasureComparisonTime(expected, earlyMismatch, iterations);

    // Time comparison that differs on last character
    var lateMismatch = "correct-api-key-1234x";
    var lateTimes = MeasureComparisonTime(expected, lateMismatch, iterations);

    // Times should be approximately equal (within margin of error)
    var earlyAvg = earlyTimes.Average();
    var lateAvg = lateTimes.Average();
    var difference = Math.Abs(earlyAvg - lateAvg);

    // Allow 10% variance for system noise
    Assert.True(difference < earlyAvg * 0.1,
        $"Timing variance too high: {difference}ms (early: {earlyAvg}ms, late: {lateAvg}ms)");
}
```

</details>

---

## Security Considerations

- Ensure API key has sufficient entropy (recommend 32+ characters)
- Consider rotating API keys periodically
- Monitor for brute-force attempts on task endpoints
- Rate limit the task endpoints as additional protection

---

## Checklist

- [ ] Import `System.Security.Cryptography` namespace
- [ ] Add `ConstantTimeEquals` helper method
- [ ] Update API key comparison to use constant-time method
- [ ] Manual testing complete
- [ ] Code review approved
- [ ] Deployed to production
- [ ] Verified in production

### Future (when test projects added)
- [ ] Write unit tests
- [ ] Consider timing analysis test

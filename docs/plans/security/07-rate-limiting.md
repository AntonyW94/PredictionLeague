# Fix 07: Rate Limiting

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P2 - Medium** - Fix soon

## Severity
**Medium** - Defense in depth against brute-force and DoS attacks

## CWE Reference
[CWE-770: Allocation of Resources Without Limits or Throttling](https://cwe.mitre.org/data/definitions/770.html)

---

## Problem Description

The application has no rate limiting configured on any endpoints. This allows attackers to:
- Brute-force login credentials
- Enumerate user accounts
- Launch denial-of-service attacks
- Scrape data at high volume

### Current State

No rate limiting middleware found in `Program.cs` or any controllers.

---

## Solution

### Option A: ASP.NET Core 7+ Built-in Rate Limiting (Recommended)

.NET 7+ includes built-in rate limiting middleware which is simpler to configure.

**Step 1: Configure Rate Limiting**

**File**: `PredictionLeague.API/Program.cs`

```csharp
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add rate limiting services
builder.Services.AddRateLimiter(options =>
{
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Authentication endpoints - stricter limits
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // API endpoints - moderate limits
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

    // Configure rejection response
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter =
            ((int)context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? retryAfter.TotalSeconds
                : 60).ToString();

        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.",
            cancellationToken);
    };
});

var app = builder.Build();

// Add rate limiting middleware (before authentication)
app.UseRateLimiter();
```

**Step 2: Apply Policies to Controllers**

**File**: `PredictionLeague.API/Controllers/AuthController.cs`

```csharp
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]  // Apply auth rate limit policy
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // ...
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // ...
    }
}
```

**File**: `PredictionLeague.API/Controllers/LeaguesController.cs`

```csharp
[ApiController]
[Route("api/leagues")]
[Authorize]
[EnableRateLimiting("api")]  // Apply API rate limit policy
public class LeaguesController : BaseController
{
    // ...
}
```

---

### Option B: AspNetCoreRateLimit Package

For more advanced features or .NET 6 compatibility.

**Step 1: Install Package**

```bash
dotnet add PredictionLeague.API package AspNetCoreRateLimit
```

**Step 2: Configure Services**

**File**: `PredictionLeague.API/Program.cs`

```csharp
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// Load rate limit configuration
builder.Services.AddOptions();
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(
    builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(
    builder.Configuration.GetSection("IpRateLimitPolicies"));

// Add rate limiting services
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

var app = builder.Build();

// Add rate limiting middleware (before other middleware)
app.UseIpRateLimiting();
```

**Step 3: Configure appsettings.json**

**File**: `PredictionLeague.API/appsettings.json`

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Forwarded-For",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "QuotaExceededResponse": {
      "Content": "{{ \"message\": \"Too many requests. Please try again later.\", \"retryAfter\": \"{RetryAfter}\" }}",
      "ContentType": "application/json",
      "StatusCode": 429
    },
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      },
      {
        "Endpoint": "*",
        "Period": "1h",
        "Limit": 1000
      }
    ]
  },
  "IpRateLimitPolicies": {
    "AuthPolicy": {
      "Rules": [
        {
          "Endpoint": "*:/api/auth/*",
          "Period": "5m",
          "Limit": 10
        },
        {
          "Endpoint": "*:/api/auth/login",
          "Period": "15m",
          "Limit": 5
        }
      ]
    }
  }
}
```

---

## Recommended Rate Limits

| Endpoint Category | Limit | Window | Rationale |
|------------------|-------|--------|-----------|
| Global | 100 req | 1 min | General protection |
| Login | 5 req | 15 min | Prevent credential brute-force |
| Register | 3 req | 1 hour | Prevent mass account creation |
| Password Reset | 3 req | 1 hour | Prevent email enumeration |
| API (authenticated) | 60 req | 1 min | Normal usage pattern |
| Scheduled Tasks | 10 req | 1 min | Allow automation |

---

## Considerations

### Reverse Proxy / Load Balancer

If behind a reverse proxy (nginx, Azure App Gateway, etc.), ensure the correct client IP header is used:

```csharp
options.RealIpHeader = "X-Forwarded-For";
// or
options.RealIpHeader = "X-Real-IP";
```

### Distributed Rate Limiting

For multiple server instances, use Redis instead of in-memory storage:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
builder.Services.AddRedisRateLimiting();
```

### Bypass for Trusted IPs

Allow internal services or monitoring to bypass rate limits:

```csharp
options.IpWhitelist = new List<string> { "127.0.0.1", "::1", "10.0.0.0/8" };
```

---

## Testing Requirements

### Unit Tests

```csharp
[Fact]
public async Task Login_ExceedsRateLimit_Returns429()
{
    // Arrange
    var client = _factory.CreateClient();
    var request = new LoginRequest("test@example.com", "password");

    // Act - Make 6 requests (limit is 5)
    for (int i = 0; i < 5; i++)
    {
        await client.PostAsJsonAsync("/api/auth/login", request);
    }

    var response = await client.PostAsJsonAsync("/api/auth/login", request);

    // Assert
    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    Assert.True(response.Headers.Contains("Retry-After"));
}
```

### Integration Tests

1. Make requests up to the limit - should succeed
2. Exceed the limit - should return 429
3. Wait for window to expire - should succeed again
4. Verify Retry-After header is present

### Load Testing

Use a tool like k6, Artillery, or Apache Bench to verify rate limiting works under load:

```javascript
// k6 script
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  vus: 50,
  duration: '30s',
};

export default function () {
  const res = http.get('https://predictionleague.com/api/leagues');

  check(res, {
    'rate limited after threshold': (r) =>
      r.status === 200 || r.status === 429,
  });
}
```

---

## Checklist

- [ ] Choose implementation (built-in vs package)
- [ ] Add rate limiting services to DI
- [ ] Configure rate limit policies
- [ ] Add middleware to pipeline
- [ ] Apply policies to auth endpoints
- [ ] Apply policies to API endpoints
- [ ] Configure correct IP header for proxy
- [ ] Consider distributed storage if multi-instance
- [ ] Add IP whitelist for internal services
- [ ] Write integration tests
- [ ] Load test to verify limits
- [ ] Monitor 429 responses in production
- [ ] Code review approved
- [ ] Deployed to production

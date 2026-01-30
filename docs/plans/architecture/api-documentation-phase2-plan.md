# API Documentation Phase 2: Internal API Excellence

## Overview

| Attribute | Value |
|-----------|-------|
| **Goal** | Best-in-class internal API documentation |
| **Audience** | In-house developers only |
| **Prerequisites** | Phase 1 complete (Swagger annotations on all 65 endpoints) |
| **Current State** | Professional Swagger documentation with descriptions |
| **Target State** | Self-service documentation - developers can use API without asking questions |

---

## What Makes Great Internal API Documentation

For internal APIs, the goal is **self-service**: developers should be able to understand and use endpoints without needing to read source code or ask colleagues.

**Essential:**
- Request/response examples showing real data shapes
- Clear error response documentation
- Quick reference in Swagger UI

**Not needed for internal APIs:**
- ~~Changelog~~ - team communicates directly
- ~~Prose documentation site~~ - Swagger UI is sufficient
- ~~Multi-language code samples~~ - team uses consistent tech stack
- ~~Rate limiting docs~~ - devs can check code or ask

---

## Priority Matrix

| Priority | Feature | Impact | Effort |
|----------|---------|--------|--------|
| **P1** | Request/Response Examples | High | Medium |
| **P1** | Error Response Documentation | High | Low |
| **P2** | Enhanced Swagger Quick Start | Medium | Low |

**Total estimated effort:** 3-4 hours

---

## Task 1: Request/Response Examples

**Priority:** P1
**Effort:** Medium (2-3 hours)

Add concrete JSON examples so developers can see exactly what to send and receive.

### 1.1 Install Swashbuckle.AspNetCore.Filters

**File:** `PredictionLeague.API/PredictionLeague.API.csproj`

```xml
<PackageReference Include="Swashbuckle.AspNetCore.Filters" Version="8.0.2" />
```

### 1.2 Configure Example Filters

**File:** `PredictionLeague.API/DependencyInjection.cs`

Add to the `AddSwaggerGen` configuration:

```csharp
using Swashbuckle.AspNetCore.Filters;

// Inside AddSwaggerGen options:
options.ExampleFilters();
```

After `AddSwaggerGen`:

```csharp
services.AddSwaggerExamplesFromAssemblyOf<Program>();
```

### 1.3 Create Example Classes

Create folder: `PredictionLeague.API/Swagger/Examples/`

**Example structure:**

```csharp
// File: PredictionLeague.API/Swagger/Examples/Auth/LoginRequestExample.cs
using PredictionLeague.Contracts.Authentication;
using Swashbuckle.AspNetCore.Filters;

namespace PredictionLeague.API.Swagger.Examples.Auth;

public class LoginRequestExample : IExamplesProvider<LoginRequest>
{
    public LoginRequest GetExamples()
    {
        return new LoginRequest
        {
            Email = "john.smith@example.com",
            Password = "SecureP@ssword123"
        };
    }
}

public class AuthenticationResponseExample : IExamplesProvider<AuthenticationResponse>
{
    public AuthenticationResponse GetExamples()
    {
        return new SuccessfulAuthenticationResponse
        {
            IsSuccess = true,
            AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            RefreshToken = "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            UserId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            Email = "john.smith@example.com",
            FirstName = "John",
            LastName = "Smith",
            Role = "User"
        };
    }
}
```

### 1.4 Apply Examples to Endpoints

**File:** `PredictionLeague.API/Controllers/AuthController.cs`

```csharp
[HttpPost("login")]
[AllowAnonymous]
[SwaggerOperation(...)]
[SwaggerRequestExample(typeof(LoginRequest), typeof(LoginRequestExample))]
[SwaggerResponseExample(200, typeof(AuthenticationResponseExample))]
public async Task<IActionResult> LoginAsync(...)
```

### 1.5 Example Classes to Create

Focus on the most-used endpoints:

| Controller | Request Examples | Response Examples |
|------------|------------------|-------------------|
| AuthController | LoginRequest, RegisterRequest | AuthenticationResponse |
| LeaguesController | CreateLeagueRequest, JoinLeagueRequest, UpdateLeagueRequest | LeagueDto, LeagueMembersPageDto, LeagueDashboardDto |
| PredictionsController | SubmitPredictionsRequest | PredictionPageDto |
| BoostsController | ApplyBoostRequest | AvailableBoostDto |
| Admin/SeasonsController | CreateSeasonRequest | SeasonDto |
| Admin/RoundsController | CreateRoundRequest, SubmitResultsRequest | RoundDto, RoundDetailsDto |

---

## Task 2: Error Response Documentation

**Priority:** P1
**Effort:** Low (1 hour)

Document what error responses look like so developers can handle them properly.

### 2.1 Create Standard Error Response DTO

**File:** `PredictionLeague.Contracts/Common/ApiErrorResponse.cs`

```csharp
namespace PredictionLeague.Contracts.Common;

/// <summary>
/// Standard error response returned by all API endpoints.
/// </summary>
public record ApiErrorResponse
{
    /// <summary>
    /// Machine-readable error code (e.g., "VALIDATION_ERROR", "NOT_FOUND").
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Detailed validation errors, keyed by field name. Only present for 400 responses.
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; init; }

    /// <summary>
    /// Correlation ID for tracking in logs.
    /// </summary>
    public string? TraceId { get; init; }
}
```

### 2.2 Create Error Response Examples

**File:** `PredictionLeague.API/Swagger/Examples/Common/ErrorResponseExamples.cs`

```csharp
namespace PredictionLeague.API.Swagger.Examples.Common;

public class ValidationErrorExample : IExamplesProvider<ApiErrorResponse>
{
    public ApiErrorResponse GetExamples()
    {
        return new ApiErrorResponse
        {
            Code = "VALIDATION_ERROR",
            Message = "One or more validation errors occurred.",
            Errors = new Dictionary<string, string[]>
            {
                { "Email", new[] { "Email is required.", "Email must be a valid email address." } },
                { "Password", new[] { "Password must be at least 8 characters." } }
            },
            TraceId = "00-abc123def456-789xyz-00"
        };
    }
}

public class NotFoundErrorExample : IExamplesProvider<ApiErrorResponse>
{
    public ApiErrorResponse GetExamples()
    {
        return new ApiErrorResponse
        {
            Code = "NOT_FOUND",
            Message = "League with ID 999 was not found.",
            TraceId = "00-abc123def456-789xyz-00"
        };
    }
}

public class UnauthorisedErrorExample : IExamplesProvider<ApiErrorResponse>
{
    public ApiErrorResponse GetExamples()
    {
        return new ApiErrorResponse
        {
            Code = "UNAUTHORISED",
            Message = "Authentication is required to access this resource.",
            TraceId = "00-abc123def456-789xyz-00"
        };
    }
}

public class ForbiddenErrorExample : IExamplesProvider<ApiErrorResponse>
{
    public ApiErrorResponse GetExamples()
    {
        return new ApiErrorResponse
        {
            Code = "FORBIDDEN",
            Message = "You do not have permission to access this resource.",
            TraceId = "00-abc123def456-789xyz-00"
        };
    }
}
```

### 2.3 Update SwaggerResponse Attributes

Update error responses across all controllers to include the type:

```csharp
[SwaggerResponse(400, "Validation failed", typeof(ApiErrorResponse))]
[SwaggerResponseExample(400, typeof(ValidationErrorExample))]
[SwaggerResponse(401, "Not authenticated", typeof(ApiErrorResponse))]
[SwaggerResponse(403, "Not authorised", typeof(ApiErrorResponse))]
[SwaggerResponse(404, "Not found", typeof(ApiErrorResponse))]
```

---

## Task 3: Enhanced Swagger Quick Start

**Priority:** P2
**Effort:** Low (30 minutes)

Add a quick reference guide directly in Swagger UI for easy onboarding.

### 3.1 Update API Description

**File:** `PredictionLeague.API/DependencyInjection.cs`

Update the SwaggerDoc description to include a quick start:

```csharp
Description = @"
## Quick Start

### 1. Authenticate
```
POST /api/auth/login
{ ""email"": ""your@email.com"", ""password"": ""your-password"" }
```

### 2. Use the Token
Include the `accessToken` in all requests:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### 3. Token Refresh
Access tokens expire after **15 minutes**. Use `/api/auth/refresh-token` to get a new one.

---

## Authentication

| Type | Usage |
|------|-------|
| **JWT Bearer** | Most endpoints - token from login |
| **API Key** | `/api/tasks/*` endpoints only (cron jobs) |

## Rate Limits

| Category | Limit |
|----------|-------|
| Auth endpoints | 10 req / 5 min |
| General API | 60 req / min |
"
```

---

## Checklist

### P1: Examples (High Impact)
- [ ] Install Swashbuckle.AspNetCore.Filters package
- [ ] Configure example filters in DependencyInjection.cs
- [ ] Create request examples for Auth endpoints
- [ ] Create request examples for League endpoints
- [ ] Create request examples for Prediction endpoints
- [ ] Create request examples for Boost endpoints
- [ ] Create request examples for Admin endpoints
- [ ] Create response examples for key DTOs
- [ ] Create ApiErrorResponse DTO
- [ ] Create error response examples (400, 401, 403, 404)
- [ ] Update SwaggerResponse attributes to include error types

### P2: Quick Start
- [ ] Update Swagger description with quick start guide

---

## Files to Create

| File | Purpose |
|------|---------|
| `PredictionLeague.API/Swagger/Examples/Auth/*.cs` | Auth request/response examples |
| `PredictionLeague.API/Swagger/Examples/Leagues/*.cs` | League request/response examples |
| `PredictionLeague.API/Swagger/Examples/Predictions/*.cs` | Prediction examples |
| `PredictionLeague.API/Swagger/Examples/Boosts/*.cs` | Boost examples |
| `PredictionLeague.API/Swagger/Examples/Admin/*.cs` | Admin endpoint examples |
| `PredictionLeague.API/Swagger/Examples/Common/*.cs` | Error response examples |
| `PredictionLeague.Contracts/Common/ApiErrorResponse.cs` | Standard error DTO |

---

## Success Criteria

When complete, your internal API documentation will enable:

1. **Self-service** - Developers can use any endpoint without reading source code
2. **Copy-paste examples** - Real JSON shapes visible in Swagger UI
3. **Error handling** - Clear examples of what errors look like
4. **Quick onboarding** - New team members productive immediately

This is the gold standard for internal API documentation.

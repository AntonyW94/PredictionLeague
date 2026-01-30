# API Documentation Phase 2: Industry-Leading Documentation Plan

## Overview

| Attribute | Value |
|-----------|-------|
| **Goal** | Elevate API documentation to industry-leading standards (Stripe/Twilio level) |
| **Prerequisites** | Phase 1 complete (Swagger annotations on all 65 endpoints) |
| **Current State** | Professional Swagger documentation with descriptions |
| **Target State** | Comprehensive documentation with examples, guides, and developer experience features |

---

## Priority Matrix

| Priority | Feature | Impact | Effort |
|----------|---------|--------|--------|
| **P1** | Request/Response Examples | High | Medium |
| **P1** | Error Response Documentation | High | Low |
| **P2** | Getting Started Guide | High | Medium |
| **P2** | Authentication Guide | High | Low |
| **P3** | Code Samples (curl, C#, JS) | Medium | High |
| **P3** | Changelog | Medium | Low |
| **P4** | Prose Documentation Site | Medium | High |
| **P4** | Rate Limiting per Endpoint | Low | Low |

---

## Task 1: Request/Response Examples

**Priority:** P1
**Effort:** Medium (2-3 hours)

Add concrete JSON examples to help developers understand exactly what to send and what they'll receive.

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

Create a new folder: `PredictionLeague.API/Swagger/Examples/`

**Example structure for AuthController:**

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

### 1.5 Example Classes Needed (Priority Endpoints)

Create examples for the most commonly used endpoints first:

| Controller | Request Examples | Response Examples |
|------------|------------------|-------------------|
| AuthController | LoginRequest, RegisterRequest, RefreshTokenRequest | AuthenticationResponse |
| LeaguesController | CreateLeagueRequest, JoinLeagueRequest, UpdateLeagueRequest | LeagueDto, LeagueMembersPageDto |
| PredictionsController | SubmitPredictionsRequest | PredictionPageDto |
| BoostsController | ApplyBoostRequest | AvailableBoostDto, ApplyBoostResultDto |
| Admin/SeasonsController | CreateSeasonRequest | SeasonDto |
| Admin/RoundsController | CreateRoundRequest, UpdateRoundRequest | RoundDto, RoundDetailsDto |

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
    /// Correlation ID for support requests.
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

Update error responses to include the type:

```csharp
[SwaggerResponse(400, "Validation failed", typeof(ApiErrorResponse))]
[SwaggerResponseExample(400, typeof(ValidationErrorExample))]
[SwaggerResponse(401, "Not authenticated", typeof(ApiErrorResponse))]
[SwaggerResponse(403, "Not authorised", typeof(ApiErrorResponse))]
[SwaggerResponse(404, "Not found", typeof(ApiErrorResponse))]
```

---

## Task 3: Getting Started Guide

**Priority:** P2
**Effort:** Medium (1-2 hours)

Create a quick-start guide in the Swagger description or a separate markdown file.

### 3.1 Enhanced API Description

**File:** `PredictionLeague.API/DependencyInjection.cs`

Update the SwaggerDoc description:

```csharp
Description = @"
## Quick Start

### 1. Register or Login
```bash
POST /api/auth/register
{
  ""firstName"": ""John"",
  ""lastName"": ""Smith"",
  ""email"": ""john@example.com"",
  ""password"": ""SecureP@ss123!""
}
```

### 2. Use the Access Token
Include the `accessToken` from the response in all subsequent requests:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### 3. Join or Create a League
```bash
# Join with entry code
POST /api/leagues/join
{ ""entryCode"": ""ABC123"" }

# Or create your own
POST /api/leagues/create
{ ""name"": ""My League"", ""seasonId"": 1, ""price"": 10.00 }
```

### 4. Submit Predictions
```bash
POST /api/predictions/submit
{
  ""roundId"": 1,
  ""predictions"": [
    { ""matchId"": 1, ""homeScore"": 2, ""awayScore"": 1 },
    { ""matchId"": 2, ""homeScore"": 0, ""awayScore"": 0 }
  ]
}
```

---

## Authentication

Most endpoints require **JWT Bearer** authentication:
1. Call `/api/auth/login` or `/api/auth/register`
2. Copy the `accessToken` from the response
3. Click **Authorize** above and paste the token
4. Access tokens expire after **15 minutes** - use `/api/auth/refresh-token` to get a new one

## Scheduled Tasks (Internal)

Endpoints under `/api/tasks/` require **API Key** authentication via `X-Api-Key` header.
These are for cron jobs and should not be called by regular users.

## Rate Limits

| Endpoint Type | Limit |
|--------------|-------|
| Global | 100 requests/minute/IP |
| Auth endpoints | 10 requests/5 minutes/IP |
| API endpoints | 60 requests/minute/IP |

## Support

- **Email:** support@thepredictions.co.uk
- **Website:** https://www.thepredictions.co.uk
"
```

### 3.2 Create Standalone Getting Started Guide (Optional)

**File:** `docs/api/getting-started.md`

A more detailed guide with:
- Prerequisites
- Step-by-step walkthrough with screenshots
- Common use cases
- Troubleshooting

---

## Task 4: Authentication Guide

**Priority:** P2
**Effort:** Low (30 minutes)

### 4.1 Create Auth Documentation Page

**File:** `docs/api/authentication.md`

```markdown
# Authentication Guide

## Overview

The PredictionLeague API uses JWT (JSON Web Tokens) for authentication.

## Token Types

| Token | Location | Expiry | Purpose |
|-------|----------|--------|---------|
| Access Token | Authorization header | 15 minutes | API access |
| Refresh Token | HTTP-only cookie + response body | 7 days | Obtain new access tokens |

## Authentication Flow

### 1. Initial Login

```bash
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "your-password"
}
```

Response:
```json
{
  "isSuccess": true,
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2...",
  "expiresAt": "2026-01-30T15:30:00Z",
  "userId": "abc123",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Smith"
}
```

### 2. Using the Access Token

Include in all authenticated requests:

```bash
GET /api/dashboard/my-leagues
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### 3. Refreshing Tokens

When your access token expires (401 response), refresh it:

```bash
POST /api/auth/refresh-token
Content-Type: application/json

{
  "token": "dGhpcyBpcyBhIHJlZnJlc2..."
}
```

### 4. Logout

```bash
POST /api/auth/logout
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

## Google OAuth

For Google sign-in, redirect users to:
```
GET /external-auth/google-login?returnUrl=/dashboard
```

The user will be redirected back with a refresh token in the URL.

## Security Best Practices

1. **Never store access tokens in localStorage** - Use memory or secure cookies
2. **Implement token refresh** - Don't wait for 401, refresh proactively
3. **Handle refresh failures** - Redirect to login if refresh fails
4. **Use HTTPS only** - Never send tokens over HTTP
```

---

## Task 5: Code Samples

**Priority:** P3
**Effort:** High (3-4 hours)

Add code samples in multiple languages for key endpoints.

### 5.1 Create Code Samples Directory

```
docs/api/samples/
├── curl/
│   ├── auth.md
│   ├── leagues.md
│   └── predictions.md
├── csharp/
│   ├── auth.md
│   ├── leagues.md
│   └── predictions.md
└── javascript/
    ├── auth.md
    ├── leagues.md
    └── predictions.md
```

### 5.2 Example: Authentication Samples

**File:** `docs/api/samples/curl/auth.md`

```markdown
# Authentication - curl Examples

## Register
```bash
curl -X POST https://api.thepredictions.co.uk/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName": "Smith",
    "email": "john@example.com",
    "password": "SecureP@ss123!"
  }'
```

## Login
```bash
curl -X POST https://api.thepredictions.co.uk/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john@example.com",
    "password": "SecureP@ss123!"
  }'
```

## Refresh Token
```bash
curl -X POST https://api.thepredictions.co.uk/api/auth/refresh-token \
  -H "Content-Type: application/json" \
  -d '{
    "token": "your-refresh-token"
  }'
```
```

**File:** `docs/api/samples/csharp/auth.md`

```markdown
# Authentication - C# Examples

## Using HttpClient

```csharp
using System.Net.Http.Json;

public class PredictionLeagueClient
{
    private readonly HttpClient _client;
    private string? _accessToken;

    public PredictionLeagueClient(string baseUrl)
    {
        _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        _accessToken = result?.AccessToken;
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);

        return result!;
    }

    public async Task<IEnumerable<MyLeagueDto>> GetMyLeaguesAsync()
    {
        return await _client.GetFromJsonAsync<IEnumerable<MyLeagueDto>>(
            "/api/dashboard/my-leagues") ?? [];
    }
}
```
```

**File:** `docs/api/samples/javascript/auth.md`

```markdown
# Authentication - JavaScript Examples

## Using fetch

```javascript
const API_BASE = 'https://api.thepredictions.co.uk';

class PredictionLeagueClient {
  #accessToken = null;

  async login(email, password) {
    const response = await fetch(`${API_BASE}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    if (!response.ok) {
      throw new Error(`Login failed: ${response.status}`);
    }

    const data = await response.json();
    this.#accessToken = data.accessToken;
    return data;
  }

  async getMyLeagues() {
    const response = await fetch(`${API_BASE}/api/dashboard/my-leagues`, {
      headers: {
        'Authorization': `Bearer ${this.#accessToken}`
      }
    });

    if (!response.ok) {
      throw new Error(`Request failed: ${response.status}`);
    }

    return response.json();
  }
}

// Usage
const client = new PredictionLeagueClient();
await client.login('john@example.com', 'password123');
const leagues = await client.getMyLeagues();
console.log(leagues);
```
```

---

## Task 6: Changelog

**Priority:** P3
**Effort:** Low (30 minutes setup, ongoing maintenance)

### 6.1 Create Changelog File

**File:** `docs/api/CHANGELOG.md`

```markdown
# API Changelog

All notable changes to the PredictionLeague API will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this API adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive Swagger documentation with examples

---

## [1.0.0] - 2026-01-30

### Added
- Initial API release
- Authentication endpoints (register, login, logout, refresh-token)
- Google OAuth integration
- League management (create, join, update, delete)
- Predictions submission
- Leaderboards (overall, monthly, exact scores)
- Boost system (Double Up)
- Admin endpoints for seasons, rounds, teams, and users

### Security
- JWT Bearer authentication
- API Key authentication for scheduled tasks
- Rate limiting on all endpoints
```

### 6.2 Add Changelog Link to Swagger

**File:** `PredictionLeague.API/DependencyInjection.cs`

Add to the description:
```csharp
## Changelog

See [API Changelog](/docs/api/CHANGELOG.md) for version history and breaking changes.
```

---

## Task 7: Rate Limiting Documentation

**Priority:** P4
**Effort:** Low (30 minutes)

### 7.1 Document Rate Limits per Endpoint Category

**File:** `docs/api/rate-limits.md`

```markdown
# Rate Limiting

## Overview

The API implements rate limiting to ensure fair usage and protect against abuse.

## Limits by Endpoint

| Category | Endpoints | Limit | Window |
|----------|-----------|-------|--------|
| **Authentication** | `/api/auth/*` | 10 requests | 5 minutes |
| **General API** | `/api/*` (except auth) | 60 requests | 1 minute |
| **Scheduled Tasks** | `/api/tasks/*` | Unlimited | N/A |

## Response Headers

Rate limit information is included in response headers:

```
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 45
X-RateLimit-Reset: 1706630400
```

## Exceeding Limits

When you exceed the rate limit, you'll receive:

```
HTTP/1.1 429 Too Many Requests
Retry-After: 30

{
  "code": "RATE_LIMIT_EXCEEDED",
  "message": "Too many requests. Please wait 30 seconds before retrying.",
  "retryAfter": 30
}
```

## Best Practices

1. **Implement exponential backoff** - When you receive a 429, wait before retrying
2. **Cache responses** - Reduce API calls by caching where appropriate
3. **Use webhooks** - For real-time updates instead of polling (future feature)
4. **Batch requests** - Combine multiple operations where possible
```

---

## Task 8: Prose Documentation Site (Future)

**Priority:** P4
**Effort:** High (8+ hours)

For truly industry-leading documentation, consider a dedicated documentation site.

### 8.1 Recommended Tools

| Tool | Pros | Cons |
|------|------|------|
| **Docusaurus** | React-based, versioning, search | Requires Node.js |
| **MkDocs** | Python, Material theme, simple | Less interactive |
| **GitBook** | Hosted, collaborative | Paid for private repos |
| **Readme.io** | API-focused, interactive | Paid service |

### 8.2 Recommended Structure

```
docs-site/
├── docs/
│   ├── getting-started/
│   │   ├── quick-start.md
│   │   ├── authentication.md
│   │   └── first-prediction.md
│   ├── guides/
│   │   ├── creating-leagues.md
│   │   ├── submitting-predictions.md
│   │   ├── understanding-scoring.md
│   │   └── using-boosts.md
│   ├── api-reference/
│   │   ├── authentication.md
│   │   ├── leagues.md
│   │   ├── predictions.md
│   │   └── admin.md
│   ├── sdks/
│   │   ├── javascript.md
│   │   └── csharp.md
│   └── resources/
│       ├── changelog.md
│       ├── rate-limits.md
│       └── error-codes.md
└── static/
    └── openapi.json
```

---

## Checklist

### P1: High Impact, Quick Wins
- [ ] Install Swashbuckle.AspNetCore.Filters package
- [ ] Configure example filters in DependencyInjection.cs
- [ ] Create request examples for Auth endpoints
- [ ] Create request examples for League endpoints
- [ ] Create request examples for Prediction endpoints
- [ ] Create response examples for key DTOs
- [ ] Create ApiErrorResponse DTO
- [ ] Create error response examples (400, 401, 403, 404)
- [ ] Update SwaggerResponse to include error types

### P2: Guides
- [ ] Update Swagger description with Quick Start guide
- [ ] Create standalone authentication guide
- [ ] Create getting-started.md

### P3: Code Samples & Changelog
- [ ] Create curl samples for key endpoints
- [ ] Create C# samples for key endpoints
- [ ] Create JavaScript samples for key endpoints
- [ ] Create CHANGELOG.md
- [ ] Add changelog link to Swagger

### P4: Advanced (Future)
- [ ] Document rate limits per endpoint
- [ ] Add rate limit headers to responses
- [ ] Evaluate documentation site options
- [ ] Build documentation site

---

## Files to Create

| File | Purpose |
|------|---------|
| `PredictionLeague.API/Swagger/Examples/Auth/*.cs` | Auth request/response examples |
| `PredictionLeague.API/Swagger/Examples/Leagues/*.cs` | League request/response examples |
| `PredictionLeague.API/Swagger/Examples/Predictions/*.cs` | Prediction examples |
| `PredictionLeague.API/Swagger/Examples/Common/*.cs` | Error response examples |
| `PredictionLeague.Contracts/Common/ApiErrorResponse.cs` | Standard error DTO |
| `docs/api/getting-started.md` | Quick start guide |
| `docs/api/authentication.md` | Auth detailed guide |
| `docs/api/rate-limits.md` | Rate limiting documentation |
| `docs/api/CHANGELOG.md` | API version history |
| `docs/api/samples/curl/*.md` | curl code samples |
| `docs/api/samples/csharp/*.md` | C# code samples |
| `docs/api/samples/javascript/*.md` | JavaScript code samples |

---

## Success Criteria

When complete, your API documentation will have:

1. **Discoverability** - Developers can understand the API without reading code
2. **Examples** - Every endpoint has request/response examples
3. **Error Handling** - Clear documentation of what can go wrong
4. **Quick Start** - New developers productive within 5 minutes
5. **Code Samples** - Copy-paste examples in multiple languages
6. **Versioning** - Clear changelog of API evolution

This will put your documentation in the top tier alongside APIs like Stripe, Twilio, and GitHub.

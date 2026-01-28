# API Documentation Improvement Plan

## Overview

| Attribute | Value |
|-----------|-------|
| **Goal** | Industry-leading Swagger/OpenAPI documentation using attributes |
| **Approach** | Swashbuckle.AspNetCore.Annotations (no XML comments) |
| **Current State** | Basic auto-generated Swagger with no descriptions |
| **Endpoints** | 65 total across 14 controllers |
| **Estimated Effort** | 4-6 hours |

---

## Execution Instructions

> **IMPORTANT:** When executing this plan, confirm each endpoint with the user before moving to the next one. Present the proposed attributes and wait for approval or corrections before implementing. This ensures descriptions accurately reflect the business logic.

**Workflow for each endpoint:**
1. Show the current endpoint signature
2. Present the proposed Swagger attributes
3. Wait for user confirmation or corrections
4. Implement the approved attributes
5. Move to the next endpoint

---

## Tasks

### Task 1: Install Swashbuckle.AspNetCore.Annotations Package

**File:** `PredictionLeague.API/PredictionLeague.API.csproj`

Add package reference:
```xml
<PackageReference Include="Swashbuckle.AspNetCore.Annotations" Version="7.2.0" />
```

---

### Task 2: Configure Swagger with Security Schemes and Annotations

**File:** `PredictionLeague.API/DependencyInjection.cs`

**Required usings:**
```csharp
using Microsoft.OpenApi.Models;
using System.Reflection;
```

Replace `services.AddSwaggerGen();` with the full configuration below:

```csharp
services.AddSwaggerGen(options =>
{
    // ===========================================
    // API INFORMATION
    // ===========================================
    // This appears at the top of Swagger UI and in the OpenAPI spec
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "PredictionLeague API",
        Description = @"
## Overview
API for the PredictionLeague football prediction platform. Allows users to create leagues,
submit match predictions, track leaderboards, and manage prizes.

## Authentication
Most endpoints require JWT Bearer authentication. Include the token in the Authorization header:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

To obtain a token, use the `/api/auth/login` or `/api/auth/register` endpoints.

## Scheduled Tasks
Endpoints under `/api/tasks/` require API Key authentication via the `X-Api-Key` header.
These are intended for scheduled jobs (cron) and should not be called directly by users.

## Rate Limiting
- **Global:** 100 requests per minute per IP
- **Auth endpoints:** 10 requests per 5 minutes per IP
- **API endpoints:** 60 requests per minute per IP
",
        Contact = new OpenApiContact
        {
            Name = "PredictionLeague Support",
            Email = "support@thepredictions.co.uk",
            Url = new Uri("https://www.thepredictions.co.uk")
        },
        License = new OpenApiLicense
        {
            Name = "Proprietary",
            Url = new Uri("https://www.thepredictions.co.uk/terms")
        }
    });

    // ===========================================
    // ENABLE ANNOTATIONS
    // ===========================================
    // This enables [SwaggerOperation], [SwaggerResponse], [SwaggerParameter] attributes
    options.EnableAnnotations();

    // ===========================================
    // JWT BEARER AUTHENTICATION
    // ===========================================
    // This adds the "Authorize" button to Swagger UI for JWT tokens
    //
    // How it works:
    // 1. User clicks "Authorize" button in Swagger UI
    // 2. User enters their JWT token (without "Bearer " prefix)
    // 3. Swagger automatically adds "Authorization: Bearer {token}" header to all requests
    // 4. Protected endpoints can now be tested directly in Swagger UI
    //
    // The SecuritySchemeType.Http with "bearer" scheme handles the "Bearer " prefix automatically
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = @"JWT Bearer token authentication.

**How to authenticate:**
1. Call `/api/auth/login` with your credentials
2. Copy the `accessToken` from the response
3. Click 'Authorize' and paste the token (without 'Bearer ' prefix)
4. Click 'Authorize' to apply

Token expires after 15 minutes. Use `/api/auth/refresh-token` to obtain a new token.",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    // Apply JWT authentication requirement globally
    // This makes all endpoints show the padlock icon by default
    // Individual endpoints can override this with [AllowAnonymous] or custom requirements
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // ===========================================
    // API KEY AUTHENTICATION (for scheduled tasks)
    // ===========================================
    // This adds a second authentication option for API key-protected endpoints
    //
    // How it works:
    // 1. Scheduled task endpoints (TasksController) use [ApiKeyAuthorise] attribute
    // 2. These endpoints expect "X-Api-Key" header with a valid API key
    // 3. In Swagger UI, user clicks "Authorize" and enters the API key
    // 4. Swagger adds "X-Api-Key: {key}" header to requests
    //
    // Note: API key endpoints don't require JWT - they use a separate auth mechanism
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Description = @"API Key authentication for scheduled task endpoints.

**Usage:**
This is used by cron jobs to trigger scheduled tasks like:
- Score updates from football API
- Email reminders
- Data synchronization

Regular users should not need to use these endpoints.",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Scheme = "ApiKeyScheme"
    });

    // ===========================================
    // OPERATION FILTERS (Optional enhancements)
    // ===========================================
    // Tag endpoints by controller for better organization in Swagger UI
    options.TagActionsBy(api =>
    {
        if (api.GroupName != null)
            return new[] { api.GroupName };

        var controllerName = api.ActionDescriptor.RouteValues["controller"];

        // Group admin controllers under "Admin" prefix
        if (api.RelativePath?.StartsWith("api/admin/") == true)
            return new[] { $"Admin - {controllerName}" };

        return new[] { controllerName ?? "Default" };
    });

    // Sort tags alphabetically
    options.OrderActionsBy(api => api.RelativePath);
});
```

---

### Task 3: Add Controller-Level Tags

Add `[SwaggerTag]` to each controller to provide category descriptions in Swagger UI.

**Required using in each controller:**
```csharp
using Swashbuckle.AspNetCore.Annotations;
```

**Controller tags:**

```csharp
// AuthController.cs
[SwaggerTag("Authentication - Register, login, logout, and token refresh")]

// ExternalAuthController.cs
[SwaggerTag("Authentication - OAuth login with Google")]

// AccountController.cs
[SwaggerTag("Account - Manage user profile and settings")]

// DashboardController.cs
[SwaggerTag("Dashboard - Aggregated data for the main dashboard view")]

// LeaguesController.cs
[SwaggerTag("Leagues - Create, join, and manage prediction leagues")]

// PredictionsController.cs
[SwaggerTag("Predictions - Submit and view match predictions")]

// RoundsController.cs
[SwaggerTag("Rounds - View round and match information")]

// BoostsController.cs
[SwaggerTag("Boosts - Apply prediction boosts like Double Up")]

// TasksController.cs
[SwaggerTag("Scheduled Tasks - Background job endpoints (API Key required)")]

// Admin/SeasonsController.cs
[SwaggerTag("Admin - Seasons - Manage football seasons")]

// Admin/RoundsController.cs
[SwaggerTag("Admin - Rounds - Manage gameweeks and matches")]

// Admin/TeamsController.cs
[SwaggerTag("Admin - Teams - Manage football teams")]

// Admin/UsersController.cs
[SwaggerTag("Admin - Users - User administration")]
```

---

### Task 4: Add Endpoint Attributes

Each endpoint needs the following attributes:

| Attribute | Purpose | Required |
|-----------|---------|----------|
| `[SwaggerOperation]` | Summary and detailed description | Yes |
| `[SwaggerResponse]` | Document each possible HTTP status code | Yes (all codes) |
| `[SwaggerParameter]` | Describe route/query parameters | Yes (if has params) |

**Standard response codes by endpoint type:**

| Endpoint Type | Response Codes |
|--------------|----------------|
| GET (single item) | 200, 401, 403, 404 |
| GET (list) | 200, 401, 403 |
| POST (create) | 201, 400, 401, 403 |
| POST (action) | 200, 400, 401, 403, 404 |
| PUT (update) | 200, 400, 401, 403, 404 |
| DELETE | 200, 401, 403, 404 |
| Anonymous | Omit 401 |
| Admin only | Add 403 with "Admin access required" |

---

#### 4.1 AuthController (4 endpoints)

**File:** `PredictionLeague.API/Controllers/AuthController.cs`

```csharp
[HttpPost("register")]
[AllowAnonymous]
[SwaggerOperation(
    Summary = "Register a new user account",
    Description = "Creates a new user account with email and password. Returns authentication tokens on success. The user is automatically logged in after registration.")]
[SwaggerResponse(200, "Registration successful - returns access token, refresh token, and user details", typeof(AuthenticationResponse))]
[SwaggerResponse(400, "Validation failed - email already exists, password too weak, or invalid input")]
public async Task<IActionResult> RegisterAsync(
    [FromBody, SwaggerParameter("Registration details including email, password, first name, and last name", Required = true)] RegisterRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("login")]
[AllowAnonymous]
[SwaggerOperation(
    Summary = "Authenticate with email and password",
    Description = "Validates credentials and returns authentication tokens. Access token expires in 15 minutes. Refresh token is set as HTTP-only cookie and also returned in response body.")]
[SwaggerResponse(200, "Login successful - returns access token, refresh token, and user details", typeof(AuthenticationResponse))]
[SwaggerResponse(400, "Invalid credentials or account locked")]
public async Task<IActionResult> LoginAsync(
    [FromBody, SwaggerParameter("Login credentials", Required = true)] LoginRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("refresh-token")]
[AllowAnonymous]
[SwaggerOperation(
    Summary = "Refresh an expired access token",
    Description = "Uses the refresh token (from HTTP-only cookie or request body) to obtain a new access token. The old refresh token is invalidated and a new one is issued (token rotation).")]
[SwaggerResponse(200, "Token refresh successful - returns new access token and refresh token", typeof(AuthenticationResponse))]
[SwaggerResponse(400, "Invalid or expired refresh token")]
public async Task<IActionResult> RefreshTokenAsync(CancellationToken cancellationToken)
```

```csharp
[HttpPost("logout")]
[SwaggerOperation(
    Summary = "Log out the current user",
    Description = "Invalidates the current refresh token and clears the refresh token cookie. The access token remains valid until expiry but should be discarded by the client.")]
[SwaggerResponse(200, "Logout successful")]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
```

---

#### 4.2 ExternalAuthController (2 endpoints)

**File:** `PredictionLeague.API/Controllers/ExternalAuthController.cs`

```csharp
[HttpGet("google-login")]
[AllowAnonymous]
[SwaggerOperation(
    Summary = "Initiate Google OAuth login",
    Description = "Redirects to Google's OAuth consent screen. After authentication, Google redirects back to the callback endpoint which then redirects to the client application with tokens.")]
[SwaggerResponse(302, "Redirect to Google OAuth")]
public IActionResult GoogleLogin(
    [FromQuery, SwaggerParameter("URL to redirect to after authentication completes")] string? returnUrl)
```

```csharp
[HttpGet("signin-google")]
[AllowAnonymous]
[SwaggerOperation(
    Summary = "Google OAuth callback (internal)",
    Description = "Callback endpoint for Google OAuth. Processes the authentication response, creates/updates user account, generates tokens, and redirects to the client application. Not intended to be called directly.")]
[SwaggerResponse(302, "Redirect to client application with tokens")]
[SwaggerResponse(400, "OAuth authentication failed")]
public async Task<IActionResult> GoogleCallback(CancellationToken cancellationToken)
```

---

#### 4.3 AccountController (2 endpoints)

**File:** `PredictionLeague.API/Controllers/AccountController.cs`

```csharp
[HttpGet("details")]
[SwaggerOperation(
    Summary = "Get current user's account details",
    Description = "Returns the authenticated user's profile information including name, email, and account settings.")]
[SwaggerResponse(200, "User details retrieved successfully", typeof(UserDetailsDto))]
[SwaggerResponse(401, "Not authenticated - valid JWT required")]
public async Task<IActionResult> GetDetailsAsync(CancellationToken cancellationToken)
```

```csharp
[HttpPut("details")]
[SwaggerOperation(
    Summary = "Update current user's account details",
    Description = "Updates the authenticated user's profile information. Only provided fields are updated.")]
[SwaggerResponse(200, "User details updated successfully", typeof(UserDetailsDto))]
[SwaggerResponse(400, "Validation failed - check error details")]
[SwaggerResponse(401, "Not authenticated - valid JWT required")]
public async Task<IActionResult> UpdateDetailsAsync(
    [FromBody, SwaggerParameter("Updated profile information", Required = true)] UpdateUserDetailsRequest request,
    CancellationToken cancellationToken)
```

---

#### 4.4 DashboardController (6 endpoints)

**File:** `PredictionLeague.API/Controllers/DashboardController.cs`

```csharp
[HttpGet("active-rounds")]
[SwaggerOperation(
    Summary = "Get active rounds for dashboard",
    Description = "Returns upcoming and in-progress rounds with match counts, deadlines, and prediction status. Used to populate the main dashboard tiles.")]
[SwaggerResponse(200, "Active rounds retrieved successfully", typeof(IEnumerable<ActiveRoundDto>))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetActiveRoundsAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("my-leagues")]
[SwaggerOperation(
    Summary = "Get user's leagues",
    Description = "Returns all leagues the current user is a member of, including pending join requests. Shows league name, member count, and user's current standing.")]
[SwaggerResponse(200, "User's leagues retrieved successfully", typeof(IEnumerable<MyLeagueDto>))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetMyLeaguesAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("available-leagues")]
[SwaggerOperation(
    Summary = "Get public leagues available to join",
    Description = "Returns public leagues for the current season that the user is not already a member of.")]
[SwaggerResponse(200, "Available leagues retrieved successfully", typeof(IEnumerable<AvailableLeagueDto>))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetAvailableLeaguesAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("private-leagues-available")]
[SwaggerOperation(
    Summary = "Check if private leagues exist",
    Description = "Returns whether any private leagues are available for the current season. Used to show/hide the 'Join Private League' option.")]
[SwaggerResponse(200, "Returns boolean indicating private league availability", typeof(bool))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetPrivateLeaguesAvailableAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("leaderboards")]
[SwaggerOperation(
    Summary = "Get leaderboard summaries",
    Description = "Returns the user's position and points across all their leagues. Used for the dashboard leaderboard summary widget.")]
[SwaggerResponse(200, "Leaderboard summaries retrieved successfully", typeof(IEnumerable<LeaderboardSummaryDto>))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetLeaderboardsAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("pending-requests")]
[SwaggerOperation(
    Summary = "Get pending join requests",
    Description = "Returns pending membership requests for leagues the current user administers. Used to show notification badges.")]
[SwaggerResponse(200, "Pending requests retrieved successfully", typeof(IEnumerable<PendingRequestDto>))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetPendingRequestsAsync(CancellationToken cancellationToken)
```

---

#### 4.5 LeaguesController (22 endpoints)

**File:** `PredictionLeague.API/Controllers/LeaguesController.cs`

```csharp
[HttpPost("create")]
[SwaggerOperation(
    Summary = "Create a new prediction league",
    Description = "Creates a new league with the specified settings. The creating user automatically becomes the league administrator and an approved member. Returns the league details including the generated 6-character entry code.")]
[SwaggerResponse(201, "League created successfully", typeof(LeagueDto))]
[SwaggerResponse(400, "Validation failed - invalid name, scoring settings, or season")]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> CreateLeagueAsync(
    [FromBody, SwaggerParameter("League configuration including name, visibility, and scoring rules", Required = true)] CreateLeagueRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet]
[SwaggerOperation(
    Summary = "Get user's leagues",
    Description = "Returns all leagues where the current user is an approved member.")]
[SwaggerResponse(200, "Leagues retrieved successfully", typeof(IEnumerable<LeagueDto>))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetLeaguesAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}")]
[SwaggerOperation(
    Summary = "Get league details",
    Description = "Returns detailed information about a specific league including settings, scoring rules, and the current user's membership status.")]
[SwaggerResponse(200, "League details retrieved successfully", typeof(LeagueDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetLeagueByIdAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/members")]
[SwaggerOperation(
    Summary = "Get league members",
    Description = "Returns all members of the league including their status (approved, pending, rejected) and join date. Only approved members can view this.")]
[SwaggerResponse(200, "Members retrieved successfully", typeof(IEnumerable<LeagueMemberDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetLeagueMembersAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("create-data")]
[SwaggerOperation(
    Summary = "Get league creation form data",
    Description = "Returns data needed to populate the league creation form including available seasons and default scoring values.")]
[SwaggerResponse(200, "Form data retrieved successfully", typeof(CreateLeagueDataDto))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetCreateLeagueDataAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/prizes")]
[SwaggerOperation(
    Summary = "Get league prize settings",
    Description = "Returns the prize distribution configuration for the league including round prizes, monthly prizes, overall prizes, and most exact scores prizes.")]
[SwaggerResponse(200, "Prize settings retrieved successfully", typeof(LeaguePrizeSettingsDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetLeaguePrizesAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/rounds/{roundId:int}/results")]
[SwaggerOperation(
    Summary = "Get round results for league",
    Description = "Returns detailed results for a specific round including each member's predictions, points scored, and ranking. Shows actual match scores and individual prediction breakdowns.")]
[SwaggerResponse(200, "Round results retrieved successfully", typeof(LeagueRoundResultsDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League or round not found")]
public async Task<IActionResult> GetLeagueRoundResultsAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    [SwaggerParameter("The unique identifier of the round", Required = true)] int roundId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/rounds-for-dashboard")]
[SwaggerOperation(
    Summary = "Get rounds for league dashboard",
    Description = "Returns a summary of rounds for the league dashboard including completed, in-progress, and upcoming rounds with basic stats.")]
[SwaggerResponse(200, "Rounds retrieved successfully", typeof(IEnumerable<LeagueRoundSummaryDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetLeagueRoundsForDashboardAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/dashboard-data")]
[SwaggerOperation(
    Summary = "Get comprehensive league dashboard data",
    Description = "Returns all data needed for the league dashboard page including recent results, standings, upcoming fixtures, and user's prediction status.")]
[SwaggerResponse(200, "Dashboard data retrieved successfully", typeof(LeagueDashboardDataDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetLeagueDashboardDataAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/months")]
[SwaggerOperation(
    Summary = "Get months with completed rounds",
    Description = "Returns a list of months that have completed rounds for monthly leaderboard filtering. Only months with at least one completed round are included.")]
[SwaggerResponse(200, "Months retrieved successfully", typeof(IEnumerable<MonthDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetLeagueMonthsAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/leaderboard/overall")]
[SwaggerOperation(
    Summary = "Get overall season leaderboard",
    Description = "Returns the league leaderboard ranked by total points accumulated across all completed rounds in the season.")]
[SwaggerResponse(200, "Leaderboard retrieved successfully", typeof(IEnumerable<LeaderboardEntryDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetOverallLeaderboardAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/leaderboard/monthly/{month:int}")]
[SwaggerOperation(
    Summary = "Get monthly leaderboard",
    Description = "Returns the league leaderboard for a specific month, ranked by points accumulated in rounds completed during that month.")]
[SwaggerResponse(200, "Monthly leaderboard retrieved successfully", typeof(IEnumerable<LeaderboardEntryDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found or no data for specified month")]
public async Task<IActionResult> GetMonthlyLeaderboardAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    [SwaggerParameter("Month number (1-12)", Required = true)] int month,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/leaderboard/exact-scores")]
[SwaggerOperation(
    Summary = "Get exact scores leaderboard",
    Description = "Returns the league leaderboard ranked by number of exact score predictions (where predicted score exactly matched actual score).")]
[SwaggerResponse(200, "Exact scores leaderboard retrieved successfully", typeof(IEnumerable<ExactScoresLeaderboardEntryDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetExactScoresLeaderboardAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{leagueId:int}/winnings")]
[SwaggerOperation(
    Summary = "Get league winnings",
    Description = "Returns all prize payouts that have been awarded in this league, including round winners, monthly winners, and special prizes.")]
[SwaggerResponse(200, "Winnings retrieved successfully", typeof(IEnumerable<WinningsDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> GetLeagueWinningsAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPut("{leagueId:int}/update")]
[SwaggerOperation(
    Summary = "Update league settings",
    Description = "Updates league configuration. Only the league administrator can perform this action. Scoring rules cannot be changed after predictions have been submitted.")]
[SwaggerResponse(200, "League updated successfully", typeof(LeagueDto))]
[SwaggerResponse(400, "Validation failed or scoring rules locked")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not the league administrator")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> UpdateLeagueAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    [FromBody, SwaggerParameter("Updated league settings", Required = true)] UpdateLeagueRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("join")]
[SwaggerOperation(
    Summary = "Join league with entry code",
    Description = "Submits a request to join a private league using a 6-character entry code. For public leagues, membership is instant. For private leagues, the request is pending until approved by an administrator.")]
[SwaggerResponse(200, "Join request submitted successfully", typeof(JoinLeagueResultDto))]
[SwaggerResponse(400, "Invalid entry code or already a member")]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> JoinLeagueAsync(
    [FromBody, SwaggerParameter("Entry code for the league", Required = true)] JoinLeagueRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("{leagueId:int}/join")]
[SwaggerOperation(
    Summary = "Join public league directly",
    Description = "Joins a public league directly without an entry code. Only works for public leagues.")]
[SwaggerResponse(200, "Joined league successfully", typeof(JoinLeagueResultDto))]
[SwaggerResponse(400, "League is private or already a member")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> JoinPublicLeagueAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("{leagueId:int}/members/{memberId}/status")]
[SwaggerOperation(
    Summary = "Update member status",
    Description = "Approves, rejects, or removes a league member. Only the league administrator can perform this action.")]
[SwaggerResponse(200, "Member status updated successfully")]
[SwaggerResponse(400, "Invalid status transition")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not the league administrator")]
[SwaggerResponse(404, "League or member not found")]
public async Task<IActionResult> UpdateMemberStatusAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    [SwaggerParameter("The unique identifier of the member", Required = true)] int memberId,
    [FromBody, SwaggerParameter("New membership status", Required = true)] UpdateMemberStatusRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("{leagueId:int}/prizes")]
[SwaggerOperation(
    Summary = "Update league prize settings",
    Description = "Configures prize distribution for the league. Only the league administrator can perform this action.")]
[SwaggerResponse(200, "Prize settings updated successfully", typeof(LeaguePrizeSettingsDto))]
[SwaggerResponse(400, "Invalid prize configuration")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not the league administrator")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> UpdateLeaguePrizesAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    [FromBody, SwaggerParameter("Prize distribution configuration", Required = true)] UpdateLeaguePrizesRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpDelete("{leagueId:int}/join-request")]
[SwaggerOperation(
    Summary = "Withdraw join request",
    Description = "Cancels a pending request to join a league. Only the user who submitted the request can withdraw it.")]
[SwaggerResponse(200, "Join request withdrawn successfully")]
[SwaggerResponse(400, "No pending request found")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> WithdrawJoinRequestAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPut("{leagueId:int}/dismiss-alert")]
[SwaggerOperation(
    Summary = "Dismiss league alert",
    Description = "Marks an alert or notification for the league as dismissed for the current user.")]
[SwaggerResponse(200, "Alert dismissed successfully")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not a member of this league")]
[SwaggerResponse(404, "League or alert not found")]
public async Task<IActionResult> DismissAlertAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    [FromBody, SwaggerParameter("Alert details to dismiss", Required = true)] DismissAlertRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpDelete("{leagueId:int}")]
[SwaggerOperation(
    Summary = "Delete league",
    Description = "Permanently deletes a league. Only the league administrator can perform this action, and only if they are the sole member.")]
[SwaggerResponse(200, "League deleted successfully")]
[SwaggerResponse(400, "Cannot delete - league has other members")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Not the league administrator")]
[SwaggerResponse(404, "League not found")]
public async Task<IActionResult> DeleteLeagueAsync(
    [SwaggerParameter("The unique identifier of the league", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

---

#### 4.6 PredictionsController (2 endpoints)

**File:** `PredictionLeague.API/Controllers/PredictionsController.cs`

```csharp
[HttpGet("{roundId:int}")]
[SwaggerOperation(
    Summary = "Get user's predictions for a round",
    Description = "Returns the current user's predictions for all matches in the specified round. Includes match details and any applied boosts.")]
[SwaggerResponse(200, "Predictions retrieved successfully", typeof(UserPredictionsDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(404, "Round not found")]
public async Task<IActionResult> GetPredictionsAsync(
    [SwaggerParameter("The unique identifier of the round", Required = true)] int roundId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("submit")]
[SwaggerOperation(
    Summary = "Submit or update predictions",
    Description = "Submits predictions for matches in a round. Can be called multiple times to update predictions until the round deadline. After the deadline, predictions are locked.")]
[SwaggerResponse(200, "Predictions submitted successfully", typeof(SubmitPredictionsResultDto))]
[SwaggerResponse(400, "Validation failed or deadline passed")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(404, "Round or match not found")]
public async Task<IActionResult> SubmitPredictionsAsync(
    [FromBody, SwaggerParameter("Predictions for matches in the round", Required = true)] SubmitPredictionsRequest request,
    CancellationToken cancellationToken)
```

---

#### 4.7 RoundsController (1 endpoint)

**File:** `PredictionLeague.API/Controllers/RoundsController.cs`

```csharp
[HttpGet("{roundId:int}/matches-data")]
[SwaggerOperation(
    Summary = "Get matches for a round",
    Description = "Returns all matches in the round with team details, kick-off times, and current scores (if available). Used for the predictions form.")]
[SwaggerResponse(200, "Match data retrieved successfully", typeof(RoundMatchesDataDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(404, "Round not found")]
public async Task<IActionResult> GetMatchesDataAsync(
    [SwaggerParameter("The unique identifier of the round", Required = true)] int roundId,
    CancellationToken cancellationToken)
```

---

#### 4.8 BoostsController (3 endpoints)

**File:** `PredictionLeague.API/Controllers/BoostsController.cs`

```csharp
[HttpGet("available")]
[SwaggerOperation(
    Summary = "Get available boosts",
    Description = "Returns boosts available to the user for a specific league and round, including remaining uses and eligibility status. Boosts like 'Double Up' multiply points for a round.")]
[SwaggerResponse(200, "Available boosts retrieved successfully", typeof(AvailableBoostsDto))]
[SwaggerResponse(401, "Not authenticated")]
public async Task<IActionResult> GetAvailableBoostsAsync(
    [FromQuery, SwaggerParameter("The league to check boost availability for", Required = true)] int leagueId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("apply")]
[SwaggerOperation(
    Summary = "Apply a boost to predictions",
    Description = "Applies a boost (e.g., Double Up) to the user's predictions for a specific round in a league. Must be applied before the round deadline. Boost effects are calculated when the round completes.")]
[SwaggerResponse(200, "Boost applied successfully", typeof(ApplyBoostResultDto))]
[SwaggerResponse(400, "Validation failed - boost not available, already used, or deadline passed")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(404, "League, round, or boost not found")]
public async Task<IActionResult> ApplyBoostAsync(
    [FromBody, SwaggerParameter("Boost application details", Required = true)] ApplyBoostRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpDelete("user/usage")]
[SwaggerOperation(
    Summary = "Remove an applied boost",
    Description = "Removes a previously applied boost from a round. Only possible before the round deadline.")]
[SwaggerResponse(200, "Boost removed successfully")]
[SwaggerResponse(400, "Deadline passed or boost not applied")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(404, "Boost usage not found")]
public async Task<IActionResult> RemoveBoostAsync(
    [FromBody, SwaggerParameter("Boost removal details", Required = true)] RemoveBoostRequest request,
    CancellationToken cancellationToken)
```

---

#### 4.9 TasksController (5 endpoints)

**File:** `PredictionLeague.API/Controllers/TasksController.cs`

**Note:** These endpoints use API Key authentication. Add this attribute to the class:

```csharp
[ApiController]
[Route("api/[controller]")]
[SwaggerTag("Scheduled Tasks - Background job endpoints (API Key required)")]
public class TasksController : ApiControllerBase
```

Each endpoint should indicate API Key requirement:

```csharp
[HttpPost("score-update")]
[ApiKeyAuthorise]
[SwaggerOperation(
    Summary = "Update live match scores",
    Description = "Fetches latest match scores from the football API and updates the database. Triggers score recalculation for affected predictions. Called by cron job every minute during match days.",
    Tags = new[] { "Scheduled Tasks" })]
[SwaggerResponse(200, "Scores updated successfully - returns count of updated matches")]
[SwaggerResponse(401, "API key missing or invalid")]
public async Task<IActionResult> ScoreUpdateAsync(CancellationToken cancellationToken)
```

```csharp
[HttpPost("sync")]
[ApiKeyAuthorise]
[SwaggerOperation(
    Summary = "Sync season data",
    Description = "Synchronises teams and fixtures from the football API for the active season. Updates match times, adds new fixtures, and updates team information. Called by cron job daily.",
    Tags = new[] { "Scheduled Tasks" })]
[SwaggerResponse(200, "Sync completed successfully - returns sync summary")]
[SwaggerResponse(401, "API key missing or invalid")]
public async Task<IActionResult> SyncAsync(CancellationToken cancellationToken)
```

```csharp
[HttpPost("send-reminders")]
[ApiKeyAuthorise]
[SwaggerOperation(
    Summary = "Send prediction reminder emails",
    Description = "Sends email reminders to users who haven't submitted predictions for upcoming rounds. Only sends if deadline is within reminder window. Called by cron job every 30 minutes.",
    Tags = new[] { "Scheduled Tasks" })]
[SwaggerResponse(200, "Reminders sent successfully - returns count of emails sent")]
[SwaggerResponse(401, "API key missing or invalid")]
public async Task<IActionResult> SendRemindersAsync(CancellationToken cancellationToken)
```

```csharp
[HttpPost("publish-upcoming-rounds")]
[ApiKeyAuthorise]
[SwaggerOperation(
    Summary = "Publish upcoming rounds",
    Description = "Publishes draft rounds that are ready for predictions (fixtures confirmed, within publish window). Makes rounds visible to users. Called by cron job daily.",
    Tags = new[] { "Scheduled Tasks" })]
[SwaggerResponse(200, "Rounds published successfully - returns list of published round IDs")]
[SwaggerResponse(401, "API key missing or invalid")]
public async Task<IActionResult> PublishUpcomingRoundsAsync(CancellationToken cancellationToken)
```

```csharp
[HttpPost("recalculate-season-stats/{seasonId:int}")]
[ApiKeyAuthorise]
[SwaggerOperation(
    Summary = "Recalculate season statistics",
    Description = "Recalculates all points, rankings, and statistics for a season. Use as a recovery tool if data becomes inconsistent. Intensive operation - use with caution.",
    Tags = new[] { "Scheduled Tasks" })]
[SwaggerResponse(200, "Recalculation completed successfully - returns summary")]
[SwaggerResponse(401, "API key missing or invalid")]
[SwaggerResponse(404, "Season not found")]
public async Task<IActionResult> RecalculateSeasonStatsAsync(
    [SwaggerParameter("The unique identifier of the season to recalculate", Required = true)] int seasonId,
    CancellationToken cancellationToken)
```

---

#### 4.10 Admin/SeasonsController (5 endpoints)

**File:** `PredictionLeague.API/Controllers/Admin/SeasonsController.cs`

```csharp
[HttpPost("create")]
[SwaggerOperation(
    Summary = "Create a new season",
    Description = "Creates a new football season with the specified configuration. Only administrators can perform this action.")]
[SwaggerResponse(201, "Season created successfully", typeof(SeasonDto))]
[SwaggerResponse(400, "Validation failed - invalid dates or configuration")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
public async Task<IActionResult> CreateSeasonAsync(
    [FromBody, SwaggerParameter("Season configuration", Required = true)] CreateSeasonRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet]
[SwaggerOperation(
    Summary = "Get all seasons",
    Description = "Returns all seasons in the system. Only administrators can access this endpoint.")]
[SwaggerResponse(200, "Seasons retrieved successfully", typeof(IEnumerable<SeasonDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
public async Task<IActionResult> GetSeasonsAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("{seasonId:int}")]
[SwaggerOperation(
    Summary = "Get season by ID",
    Description = "Returns detailed information about a specific season including status and configuration.")]
[SwaggerResponse(200, "Season retrieved successfully", typeof(SeasonDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Season not found")]
public async Task<IActionResult> GetSeasonByIdAsync(
    [SwaggerParameter("The unique identifier of the season", Required = true)] int seasonId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPut("{seasonId:int}/update")]
[SwaggerOperation(
    Summary = "Update season details",
    Description = "Updates season configuration. Some fields may be locked after the season has started.")]
[SwaggerResponse(200, "Season updated successfully", typeof(SeasonDto))]
[SwaggerResponse(400, "Validation failed or field locked")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Season not found")]
public async Task<IActionResult> UpdateSeasonAsync(
    [SwaggerParameter("The unique identifier of the season", Required = true)] int seasonId,
    [FromBody, SwaggerParameter("Updated season data", Required = true)] UpdateSeasonRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpPut("{seasonId:int}/status")]
[SwaggerOperation(
    Summary = "Update season status",
    Description = "Changes the season status (Draft, Active, Completed). Activating a season makes it available for leagues.")]
[SwaggerResponse(200, "Season status updated successfully", typeof(SeasonDto))]
[SwaggerResponse(400, "Invalid status transition")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Season not found")]
public async Task<IActionResult> UpdateSeasonStatusAsync(
    [SwaggerParameter("The unique identifier of the season", Required = true)] int seasonId,
    [FromBody, SwaggerParameter("New status", Required = true)] UpdateSeasonStatusRequest request,
    CancellationToken cancellationToken)
```

---

#### 4.11 Admin/RoundsController (5 endpoints)

**File:** `PredictionLeague.API/Controllers/Admin/RoundsController.cs`

```csharp
[HttpPost("create")]
[SwaggerOperation(
    Summary = "Create a new round",
    Description = "Creates a new gameweek round with matches. Rounds start in Draft status and must be published to accept predictions.")]
[SwaggerResponse(201, "Round created successfully", typeof(RoundDto))]
[SwaggerResponse(400, "Validation failed - invalid matches or dates")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
public async Task<IActionResult> CreateRoundAsync(
    [FromBody, SwaggerParameter("Round configuration including matches", Required = true)] CreateRoundRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("by-season/{seasonId:int}")]
[SwaggerOperation(
    Summary = "Get rounds for a season",
    Description = "Returns all rounds for the specified season, ordered by round number.")]
[SwaggerResponse(200, "Rounds retrieved successfully", typeof(IEnumerable<RoundDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Season not found")]
public async Task<IActionResult> GetRoundsBySeasonAsync(
    [SwaggerParameter("The unique identifier of the season", Required = true)] int seasonId,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet("{roundId:int}")]
[SwaggerOperation(
    Summary = "Get round by ID",
    Description = "Returns detailed round information including all matches with team details and scores.")]
[SwaggerResponse(200, "Round retrieved successfully", typeof(RoundDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Round not found")]
public async Task<IActionResult> GetRoundByIdAsync(
    [SwaggerParameter("The unique identifier of the round", Required = true)] int roundId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPut("{roundId:int}/update")]
[SwaggerOperation(
    Summary = "Update round details",
    Description = "Updates round configuration including deadline and matches. Some fields may be locked after publication.")]
[SwaggerResponse(200, "Round updated successfully", typeof(RoundDto))]
[SwaggerResponse(400, "Validation failed or field locked")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Round not found")]
public async Task<IActionResult> UpdateRoundAsync(
    [SwaggerParameter("The unique identifier of the round", Required = true)] int roundId,
    [FromBody, SwaggerParameter("Updated round data", Required = true)] UpdateRoundRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpPut("{roundId:int}/results")]
[SwaggerOperation(
    Summary = "Update match results",
    Description = "Records final scores for matches in the round. Triggers automatic recalculation of all predictions and leaderboards.")]
[SwaggerResponse(200, "Results updated successfully", typeof(RoundDto))]
[SwaggerResponse(400, "Validation failed - invalid scores")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Round not found")]
public async Task<IActionResult> UpdateRoundResultsAsync(
    [SwaggerParameter("The unique identifier of the round", Required = true)] int roundId,
    [FromBody, SwaggerParameter("Match results", Required = true)] UpdateRoundResultsRequest request,
    CancellationToken cancellationToken)
```

---

#### 4.12 Admin/TeamsController (4 endpoints)

**File:** `PredictionLeague.API/Controllers/Admin/TeamsController.cs`

```csharp
[HttpPost("create")]
[SwaggerOperation(
    Summary = "Create a new team",
    Description = "Creates a new football team that can be used in matches.")]
[SwaggerResponse(201, "Team created successfully", typeof(TeamDto))]
[SwaggerResponse(400, "Validation failed - invalid name or duplicate")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
public async Task<IActionResult> CreateTeamAsync(
    [FromBody, SwaggerParameter("Team details", Required = true)] CreateTeamRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpGet]
[SwaggerOperation(
    Summary = "Get all teams",
    Description = "Returns all teams in the system, ordered alphabetically by name.")]
[SwaggerResponse(200, "Teams retrieved successfully", typeof(IEnumerable<TeamDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
public async Task<IActionResult> GetTeamsAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("{teamId:int}")]
[SwaggerOperation(
    Summary = "Get team by ID",
    Description = "Returns details for a specific team.")]
[SwaggerResponse(200, "Team retrieved successfully", typeof(TeamDto))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Team not found")]
public async Task<IActionResult> GetTeamByIdAsync(
    [SwaggerParameter("The unique identifier of the team", Required = true)] int teamId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPut("{teamId:int}/update")]
[SwaggerOperation(
    Summary = "Update team details",
    Description = "Updates team information such as name and short name.")]
[SwaggerResponse(200, "Team updated successfully", typeof(TeamDto))]
[SwaggerResponse(400, "Validation failed")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "Team not found")]
public async Task<IActionResult> UpdateTeamAsync(
    [SwaggerParameter("The unique identifier of the team", Required = true)] int teamId,
    [FromBody, SwaggerParameter("Updated team data", Required = true)] UpdateTeamRequest request,
    CancellationToken cancellationToken)
```

---

#### 4.13 Admin/UsersController (4 endpoints)

**File:** `PredictionLeague.API/Controllers/Admin/UsersController.cs`

```csharp
[HttpGet]
[SwaggerOperation(
    Summary = "Get all users",
    Description = "Returns all registered users in the system with their roles and status.")]
[SwaggerResponse(200, "Users retrieved successfully", typeof(IEnumerable<AdminUserDto>))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
public async Task<IActionResult> GetUsersAsync(CancellationToken cancellationToken)
```

```csharp
[HttpGet("{userId}/owns-leagues")]
[SwaggerOperation(
    Summary = "Check if user owns leagues",
    Description = "Returns whether the specified user is an administrator of any leagues. Used before user deletion.")]
[SwaggerResponse(200, "Returns boolean indicating league ownership", typeof(bool))]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "User not found")]
public async Task<IActionResult> UserOwnsLeaguesAsync(
    [SwaggerParameter("The unique identifier of the user", Required = true)] string userId,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("{userId}/role")]
[SwaggerOperation(
    Summary = "Update user role",
    Description = "Changes the role of a user (e.g., User, Admin). Administrators cannot demote themselves.")]
[SwaggerResponse(200, "Role updated successfully", typeof(AdminUserDto))]
[SwaggerResponse(400, "Invalid role or self-demotion attempt")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "User not found")]
public async Task<IActionResult> UpdateUserRoleAsync(
    [SwaggerParameter("The unique identifier of the user", Required = true)] string userId,
    [FromBody, SwaggerParameter("New role assignment", Required = true)] UpdateUserRoleRequest request,
    CancellationToken cancellationToken)
```

```csharp
[HttpPost("{userId}/delete")]
[SwaggerOperation(
    Summary = "Delete user account",
    Description = "Permanently deletes a user account. Fails if the user owns any leagues (must transfer ownership first).")]
[SwaggerResponse(200, "User deleted successfully")]
[SwaggerResponse(400, "Cannot delete - user owns leagues")]
[SwaggerResponse(401, "Not authenticated")]
[SwaggerResponse(403, "Admin access required")]
[SwaggerResponse(404, "User not found")]
public async Task<IActionResult> DeleteUserAsync(
    [SwaggerParameter("The unique identifier of the user", Required = true)] string userId,
    CancellationToken cancellationToken)
```

---

## Checklist

### Configuration
- [ ] Install Swashbuckle.AspNetCore.Annotations package
- [ ] Update DependencyInjection.cs with full Swagger configuration
- [ ] Verify Swagger UI loads with API info and authentication options

### Controller Tags (14 controllers)
- [ ] AuthController
- [ ] ExternalAuthController
- [ ] AccountController
- [ ] DashboardController
- [ ] LeaguesController
- [ ] PredictionsController
- [ ] RoundsController
- [ ] BoostsController
- [ ] TasksController
- [ ] Admin/SeasonsController
- [ ] Admin/RoundsController
- [ ] Admin/TeamsController
- [ ] Admin/UsersController

### Endpoint Attributes (65 endpoints)
- [ ] AuthController (4 endpoints) - **Confirm each with user**
- [ ] ExternalAuthController (2 endpoints) - **Confirm each with user**
- [ ] AccountController (2 endpoints) - **Confirm each with user**
- [ ] DashboardController (6 endpoints) - **Confirm each with user**
- [ ] LeaguesController (22 endpoints) - **Confirm each with user**
- [ ] PredictionsController (2 endpoints) - **Confirm each with user**
- [ ] RoundsController (1 endpoint) - **Confirm each with user**
- [ ] BoostsController (3 endpoints) - **Confirm each with user**
- [ ] TasksController (5 endpoints) - **Confirm each with user**
- [ ] Admin/SeasonsController (5 endpoints) - **Confirm each with user**
- [ ] Admin/RoundsController (5 endpoints) - **Confirm each with user**
- [ ] Admin/TeamsController (4 endpoints) - **Confirm each with user**
- [ ] Admin/UsersController (4 endpoints) - **Confirm each with user**

### Verification
- [ ] Swagger UI displays all summaries and descriptions
- [ ] JWT authentication works in Swagger UI (can login and test protected endpoints)
- [ ] API Key authentication documented for Tasks endpoints
- [ ] All response codes documented for each endpoint
- [ ] No endpoints missing attributes

---

## Files Modified

| File | Changes |
|------|---------|
| `PredictionLeague.API/PredictionLeague.API.csproj` | Add Annotations package |
| `PredictionLeague.API/DependencyInjection.cs` | Full Swagger configuration |
| `PredictionLeague.API/Controllers/AuthController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/ExternalAuthController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/AccountController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/DashboardController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/LeaguesController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/PredictionsController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/RoundsController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/BoostsController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/TasksController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/Admin/SeasonsController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/Admin/RoundsController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/Admin/TeamsController.cs` | Add Swagger attributes |
| `PredictionLeague.API/Controllers/Admin/UsersController.cs` | Add Swagger attributes |

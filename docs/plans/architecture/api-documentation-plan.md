# API Documentation Improvement Plan

## Overview

| Attribute | Value |
|-----------|-------|
| **Goal** | Industry-leading Swagger/OpenAPI documentation |
| **Current State** | Basic auto-generated Swagger with no descriptions |
| **Endpoints** | 65 total across 14 controllers |
| **Estimated Effort** | 4-6 hours |

---

## Tasks

### Task 1: Enable XML Documentation Generation

**File:** `PredictionLeague.API/PredictionLeague.API.csproj`

Add to the `<PropertyGroup>`:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);1591</NoWarn>
```

The `1591` warning suppression prevents build warnings for missing XML comments (we'll add them incrementally).

**Also add to these projects** (for DTO documentation):
- `PredictionLeague.Contracts/PredictionLeague.Contracts.csproj`

---

### Task 2: Configure Swagger with Security Schemes and API Info

**File:** `PredictionLeague.API/DependencyInjection.cs`

Replace `services.AddSwaggerGen();` with:

```csharp
services.AddSwaggerGen(options =>
{
    // API Information
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "PredictionLeague API",
        Description = "API for the PredictionLeague football prediction platform. Allows users to create leagues, submit predictions, and track leaderboards.",
        Contact = new OpenApiContact
        {
            Name = "PredictionLeague Support",
            Email = "support@thepredictions.co.uk",
            Url = new Uri("https://www.thepredictions.co.uk")
        }
    });

    // JWT Bearer Authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGciOiJIUzI1NiIs..."
    });

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

    // API Key Authentication (for scheduled tasks)
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key for scheduled task endpoints"
    });

    // Include XML comments from API project
    var apiXmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var apiXmlPath = Path.Combine(AppContext.BaseDirectory, apiXmlFile);
    if (File.Exists(apiXmlPath))
        options.IncludeXmlComments(apiXmlPath);

    // Include XML comments from Contracts project (DTOs)
    var contractsXmlPath = Path.Combine(AppContext.BaseDirectory, "PredictionLeague.Contracts.xml");
    if (File.Exists(contractsXmlPath))
        options.IncludeXmlComments(contractsXmlPath);
});
```

**Required usings:**
```csharp
using Microsoft.OpenApi.Models;
using System.Reflection;
```

---

### Task 3: Add XML Documentation to Controllers

Each endpoint needs:
- `<summary>` - Brief description of what the endpoint does
- `<param>` - Description for each parameter
- `<returns>` - Description of successful response

**Note:** `[ProducesResponseType]` attributes already exist on most endpoints, so `<response>` tags are optional.

---

#### 3.1 AuthController (4 endpoints)

**File:** `PredictionLeague.API/Controllers/AuthController.cs`

```csharp
/// <summary>
/// Registers a new user account
/// </summary>
/// <param name="request">Registration details including email, password, and name</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Authentication tokens and user details on success</returns>
[HttpPost("register")]

/// <summary>
/// Authenticates a user with email and password
/// </summary>
/// <param name="request">Login credentials</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Authentication tokens and user details on success</returns>
[HttpPost("login")]

/// <summary>
/// Refreshes an expired access token using the refresh token cookie
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>New authentication tokens</returns>
/// <remarks>Requires valid refresh token in HTTP-only cookie</remarks>
[HttpPost("refresh-token")]

/// <summary>
/// Logs out the current user by invalidating their refresh token
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Success confirmation</returns>
[HttpPost("logout")]
```

---

#### 3.2 ExternalAuthController (2 endpoints)

**File:** `PredictionLeague.API/Controllers/ExternalAuthController.cs`

```csharp
/// <summary>
/// Initiates Google OAuth login flow
/// </summary>
/// <param name="returnUrl">URL to redirect to after authentication</param>
/// <returns>Redirect to Google authentication</returns>
[HttpGet("google-login")]

/// <summary>
/// Callback endpoint for Google OAuth (internal use)
/// </summary>
/// <returns>Redirect to client with authentication tokens</returns>
[HttpGet("signin-google")]
```

---

#### 3.3 AccountController (2 endpoints)

**File:** `PredictionLeague.API/Controllers/AccountController.cs`

```csharp
/// <summary>
/// Gets the current user's account details
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>User profile information</returns>
[HttpGet("details")]

/// <summary>
/// Updates the current user's account details
/// </summary>
/// <param name="request">Updated profile information</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated user details</returns>
[HttpPut("details")]
```

---

#### 3.4 DashboardController (6 endpoints)

**File:** `PredictionLeague.API/Controllers/DashboardController.cs`

```csharp
/// <summary>
/// Gets active rounds (upcoming and in-progress) for the dashboard
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of active rounds with match counts and deadlines</returns>
[HttpGet("active-rounds")]

/// <summary>
/// Gets leagues the current user is a member of
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of user's leagues with membership status</returns>
[HttpGet("my-leagues")]

/// <summary>
/// Gets public leagues available to join
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of joinable public leagues</returns>
[HttpGet("available-leagues")]

/// <summary>
/// Checks if any private leagues are available for the current season
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Boolean indicating private league availability</returns>
[HttpGet("private-leagues-available")]

/// <summary>
/// Gets leaderboard summaries across user's leagues
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Aggregated leaderboard data</returns>
[HttpGet("leaderboards")]

/// <summary>
/// Gets pending join requests for leagues the user administers
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of pending membership requests</returns>
[HttpGet("pending-requests")]
```

---

#### 3.5 LeaguesController (22 endpoints)

**File:** `PredictionLeague.API/Controllers/LeaguesController.cs`

```csharp
/// <summary>
/// Creates a new prediction league
/// </summary>
/// <param name="request">League configuration including name, visibility, and scoring rules</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The created league with generated entry code</returns>
[HttpPost("create")]

/// <summary>
/// Gets all leagues the current user is a member of
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of user's leagues</returns>
[HttpGet]

/// <summary>
/// Gets detailed information about a specific league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>League details including settings and user's membership status</returns>
[HttpGet("{leagueId:int}")]

/// <summary>
/// Gets all members of a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of league members with their status</returns>
[HttpGet("{leagueId:int}/members")]

/// <summary>
/// Gets data required for the league creation form
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Available seasons and configuration options</returns>
[HttpGet("create-data")]

/// <summary>
/// Gets prize settings for a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Prize distribution configuration</returns>
[HttpGet("{leagueId:int}/prizes")]

/// <summary>
/// Gets round results for a specific league and round
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="roundId">The round identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Member scores and predictions for the round</returns>
[HttpGet("{leagueId:int}/rounds/{roundId:int}/results")]

/// <summary>
/// Gets rounds data for the league dashboard
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Round summaries for dashboard display</returns>
[HttpGet("{leagueId:int}/rounds-for-dashboard")]

/// <summary>
/// Gets comprehensive dashboard data for a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>League stats, recent results, and standings</returns>
[HttpGet("{leagueId:int}/dashboard-data")]

/// <summary>
/// Gets months with completed rounds for a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of months for monthly leaderboard filtering</returns>
[HttpGet("{leagueId:int}/months")]

/// <summary>
/// Gets the overall season leaderboard for a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Ranked list of members by total points</returns>
[HttpGet("{leagueId:int}/leaderboard/overall")]

/// <summary>
/// Gets the monthly leaderboard for a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="month">Month number (1-12)</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Ranked list of members by points for the specified month</returns>
[HttpGet("{leagueId:int}/leaderboard/monthly/{month:int}")]

/// <summary>
/// Gets the exact scores leaderboard for a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Ranked list of members by number of exact score predictions</returns>
[HttpGet("{leagueId:int}/leaderboard/exact-scores")]

/// <summary>
/// Gets winnings/prizes awarded in a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of prize payouts to members</returns>
[HttpGet("{leagueId:int}/winnings")]

/// <summary>
/// Updates league settings (admin only)
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="request">Updated league settings</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated league details</returns>
[HttpPut("{leagueId:int}/update")]

/// <summary>
/// Requests to join a league using an entry code
/// </summary>
/// <param name="request">Entry code for the league</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Join request status</returns>
[HttpPost("join")]

/// <summary>
/// Requests to join a specific league (for public leagues)
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Join request status</returns>
[HttpPost("{leagueId:int}/join")]

/// <summary>
/// Updates a member's status (approve/reject/remove) - admin only
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="memberId">The member identifier</param>
/// <param name="request">New membership status</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated member status</returns>
[HttpPost("{leagueId:int}/members/{memberId}/status")]

/// <summary>
/// Updates prize settings for a league (admin only)
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="request">Prize configuration</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated prize settings</returns>
[HttpPost("{leagueId:int}/prizes")]

/// <summary>
/// Withdraws a pending join request
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Confirmation of withdrawal</returns>
[HttpDelete("{leagueId:int}/join-request")]

/// <summary>
/// Dismisses an alert/notification for a league
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="request">Alert dismissal details</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Confirmation of dismissal</returns>
[HttpPut("{leagueId:int}/dismiss-alert")]

/// <summary>
/// Deletes a league (admin only, if no other members)
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Confirmation of deletion</returns>
[HttpDelete("{leagueId:int}")]
```

---

#### 3.6 PredictionsController (2 endpoints)

**File:** `PredictionLeague.API/Controllers/PredictionsController.cs`

```csharp
/// <summary>
/// Gets the user's predictions for a specific round
/// </summary>
/// <param name="roundId">The round identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>User's predictions for all matches in the round</returns>
[HttpGet("{roundId:int}")]

/// <summary>
/// Submits or updates predictions for a round
/// </summary>
/// <param name="request">Predictions for matches in the round</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Confirmation of submitted predictions</returns>
/// <remarks>Predictions can be updated until the round deadline</remarks>
[HttpPost("submit")]
```

---

#### 3.7 RoundsController (1 endpoint)

**File:** `PredictionLeague.API/Controllers/RoundsController.cs`

```csharp
/// <summary>
/// Gets match data for a round including teams and kick-off times
/// </summary>
/// <param name="roundId">The round identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Matches with team details and scheduling information</returns>
[HttpGet("{roundId:int}/matches-data")]
```

---

#### 3.8 BoostsController (3 endpoints)

**File:** `PredictionLeague.API/Controllers/BoostsController.cs`

```csharp
/// <summary>
/// Gets available boosts for the current user
/// </summary>
/// <param name="leagueId">The league identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of boosts with eligibility and usage information</returns>
[HttpGet("available")]

/// <summary>
/// Applies a boost to the user's predictions for a round
/// </summary>
/// <param name="request">Boost application details including league, round, and boost type</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Confirmation of boost application</returns>
/// <remarks>Boosts must be applied before the round deadline</remarks>
[HttpPost("apply")]

/// <summary>
/// Removes an applied boost from a round (before deadline only)
/// </summary>
/// <param name="request">Boost removal details</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Confirmation of boost removal</returns>
[HttpDelete("user/usage")]
```

---

#### 3.9 TasksController (5 endpoints)

**File:** `PredictionLeague.API/Controllers/TasksController.cs`

**Note:** These endpoints use API Key authentication via `[ApiKeyAuthorise]` attribute.

```csharp
/// <summary>
/// Updates live match scores from the football API
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Summary of updated matches</returns>
/// <remarks>Called by scheduled job every minute during matches. Requires API key.</remarks>
[HttpPost("score-update")]

/// <summary>
/// Syncs season data (teams, fixtures) from the football API
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Sync operation result</returns>
/// <remarks>Called by scheduled job daily. Requires API key.</remarks>
[HttpPost("sync")]

/// <summary>
/// Sends prediction reminder emails for upcoming deadlines
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Count of reminders sent</returns>
/// <remarks>Called by scheduled job every 30 minutes. Requires API key.</remarks>
[HttpPost("send-reminders")]

/// <summary>
/// Publishes rounds that are ready to accept predictions
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of published rounds</returns>
/// <remarks>Called by scheduled job daily. Requires API key.</remarks>
[HttpPost("publish-upcoming-rounds")]

/// <summary>
/// Recalculates all statistics for a season (admin recovery tool)
/// </summary>
/// <param name="seasonId">The season identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Recalculation summary</returns>
/// <remarks>Use with caution - intensive operation. Requires API key.</remarks>
[HttpPost("recalculate-season-stats/{seasonId:int}")]
```

---

#### 3.10 Admin/SeasonsController (5 endpoints)

**File:** `PredictionLeague.API/Controllers/Admin/SeasonsController.cs`

```csharp
/// <summary>
/// Creates a new season
/// </summary>
/// <param name="request">Season configuration</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The created season</returns>
[HttpPost("create")]

/// <summary>
/// Gets all seasons
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of all seasons</returns>
[HttpGet]

/// <summary>
/// Gets a specific season by ID
/// </summary>
/// <param name="seasonId">The season identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Season details</returns>
[HttpGet("{seasonId:int}")]

/// <summary>
/// Updates season details
/// </summary>
/// <param name="seasonId">The season identifier</param>
/// <param name="request">Updated season data</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated season</returns>
[HttpPut("{seasonId:int}/update")]

/// <summary>
/// Updates season status (draft, active, completed)
/// </summary>
/// <param name="seasonId">The season identifier</param>
/// <param name="request">New status</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated season</returns>
[HttpPut("{seasonId:int}/status")]
```

---

#### 3.11 Admin/RoundsController (5 endpoints)

**File:** `PredictionLeague.API/Controllers/Admin/RoundsController.cs`

```csharp
/// <summary>
/// Creates a new round for a season
/// </summary>
/// <param name="request">Round configuration including matches</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The created round</returns>
[HttpPost("create")]

/// <summary>
/// Gets all rounds for a season
/// </summary>
/// <param name="seasonId">The season identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of rounds</returns>
[HttpGet("by-season/{seasonId:int}")]

/// <summary>
/// Gets a specific round by ID
/// </summary>
/// <param name="roundId">The round identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Round details with matches</returns>
[HttpGet("{roundId:int}")]

/// <summary>
/// Updates round details
/// </summary>
/// <param name="roundId">The round identifier</param>
/// <param name="request">Updated round data</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated round</returns>
[HttpPut("{roundId:int}/update")]

/// <summary>
/// Updates match results for a round
/// </summary>
/// <param name="roundId">The round identifier</param>
/// <param name="request">Match results</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated round with recalculated scores</returns>
/// <remarks>Triggers score recalculation for all predictions</remarks>
[HttpPut("{roundId:int}/results")]
```

---

#### 3.12 Admin/TeamsController (4 endpoints)

**File:** `PredictionLeague.API/Controllers/Admin/TeamsController.cs`

```csharp
/// <summary>
/// Creates a new team
/// </summary>
/// <param name="request">Team details</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The created team</returns>
[HttpPost("create")]

/// <summary>
/// Gets all teams
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of all teams</returns>
[HttpGet]

/// <summary>
/// Gets a specific team by ID
/// </summary>
/// <param name="teamId">The team identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Team details</returns>
[HttpGet("{teamId:int}")]

/// <summary>
/// Updates team details
/// </summary>
/// <param name="teamId">The team identifier</param>
/// <param name="request">Updated team data</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated team</returns>
[HttpPut("{teamId:int}/update")]
```

---

#### 3.13 Admin/UsersController (4 endpoints)

**File:** `PredictionLeague.API/Controllers/Admin/UsersController.cs`

```csharp
/// <summary>
/// Gets all users (admin only)
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>List of all registered users</returns>
[HttpGet]

/// <summary>
/// Checks if a user owns any leagues
/// </summary>
/// <param name="userId">The user identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Boolean indicating league ownership</returns>
[HttpGet("{userId}/owns-leagues")]

/// <summary>
/// Updates a user's role
/// </summary>
/// <param name="userId">The user identifier</param>
/// <param name="request">New role assignment</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Updated user details</returns>
[HttpPost("{userId}/role")]

/// <summary>
/// Deletes a user account
/// </summary>
/// <param name="userId">The user identifier</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Confirmation of deletion</returns>
/// <remarks>Fails if user owns any leagues</remarks>
[HttpPost("{userId}/delete")]
```

---

### Task 4: Add Controller-Level Documentation

Add class-level XML comments to each controller:

```csharp
/// <summary>
/// Manages prediction leagues including creation, membership, and leaderboards
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LeaguesController : ApiControllerBase
```

**Apply to all controllers:**
- `AuthController` - "Handles user authentication including registration, login, and token refresh"
- `ExternalAuthController` - "Handles OAuth authentication with external providers (Google)"
- `AccountController` - "Manages user account settings and profile information"
- `DashboardController` - "Provides aggregated data for the main dashboard view"
- `LeaguesController` - "Manages prediction leagues including creation, membership, and leaderboards"
- `PredictionsController` - "Handles user match predictions"
- `RoundsController` - "Provides round and match information"
- `BoostsController` - "Manages prediction boosts (Double Up, etc.)"
- `TasksController` - "Scheduled task endpoints for background jobs (API key required)"
- `Admin/SeasonsController` - "Admin endpoints for season management"
- `Admin/RoundsController` - "Admin endpoints for round and match management"
- `Admin/TeamsController` - "Admin endpoints for team management"
- `Admin/UsersController` - "Admin endpoints for user management"

---

### Task 5: Configure API Grouping (Optional Enhancement)

Group endpoints logically in Swagger UI by adding tags:

```csharp
// In DependencyInjection.cs SwaggerGen options
options.TagActionsBy(api =>
{
    if (api.GroupName != null)
        return new[] { api.GroupName };

    if (api.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
        return new[] { controllerActionDescriptor.ControllerName };

    throw new InvalidOperationException("Unable to determine tag for endpoint.");
});

options.DocInclusionPredicate((name, api) => true);
```

Or use `[Tags("Category")]` attribute on controllers:
```csharp
[Tags("Authentication")]
public class AuthController : AuthControllerBase

[Tags("Admin - Seasons")]
public class SeasonsController : ApiControllerBase
```

---

### Task 6: Verify Swagger UI in Development

After implementing changes:

1. Run the API: `dotnet run --project PredictionLeague.API`
2. Navigate to: `https://localhost:xxxx/swagger`
3. Verify:
   - [ ] API title and description appear at top
   - [ ] "Authorize" button is visible (JWT Bearer)
   - [ ] All endpoints have descriptions
   - [ ] Parameters have descriptions
   - [ ] Response types are documented
   - [ ] Can authenticate and test protected endpoints

---

### Task 7: Enable Swagger in Production (Optional)

Currently Swagger UI is only available in Development. To enable in Production:

**File:** `PredictionLeague.API/Program.cs`

Change:
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

To:
```csharp
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PredictionLeague API v1");

    // Optional: Require authentication to view Swagger in production
    // options.OAuthClientId("swagger-ui");
});
```

**Security consideration:** If exposed in production, consider adding authentication to the Swagger endpoint or restricting by IP.

---

## Checklist

### Configuration
- [ ] Enable XML documentation in API .csproj
- [ ] Enable XML documentation in Contracts .csproj
- [ ] Configure Swagger with API info
- [ ] Configure JWT Bearer security scheme
- [ ] Configure API Key security scheme
- [ ] Include XML comment files in Swagger

### Controllers (65 endpoints total)
- [ ] AuthController (4 endpoints)
- [ ] ExternalAuthController (2 endpoints)
- [ ] AccountController (2 endpoints)
- [ ] DashboardController (6 endpoints)
- [ ] LeaguesController (22 endpoints)
- [ ] PredictionsController (2 endpoints)
- [ ] RoundsController (1 endpoint)
- [ ] BoostsController (3 endpoints)
- [ ] TasksController (5 endpoints)
- [ ] Admin/SeasonsController (5 endpoints)
- [ ] Admin/RoundsController (5 endpoints)
- [ ] Admin/TeamsController (4 endpoints)
- [ ] Admin/UsersController (4 endpoints)

### Verification
- [ ] Swagger UI displays all descriptions
- [ ] Can authenticate via Swagger UI
- [ ] Can test endpoints via Swagger UI
- [ ] No build warnings for missing XML comments (suppressed)

---

## Files Modified

| File | Changes |
|------|---------|
| `PredictionLeague.API/PredictionLeague.API.csproj` | Enable XML doc generation |
| `PredictionLeague.Contracts/PredictionLeague.Contracts.csproj` | Enable XML doc generation |
| `PredictionLeague.API/DependencyInjection.cs` | Configure Swagger |
| `PredictionLeague.API/Controllers/*.cs` | Add XML comments (14 files) |

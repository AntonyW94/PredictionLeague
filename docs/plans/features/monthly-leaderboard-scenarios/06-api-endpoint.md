# Task 6: API Endpoint

**Parent Feature:** [Monthly Leaderboard Scenarios](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add API endpoints to expose league insights and contender scenarios to the frontend.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.API/Controllers/LeaguesController.cs` | Modify | Add insights endpoints |

## Implementation Steps

### Step 1: Add Insights Endpoint to LeaguesController

```csharp
// Add to LeaguesController.cs

/// <summary>
/// Gets insights for a league's current in-progress round,
/// including contention status and win/tie probabilities.
/// </summary>
/// <param name="leagueId">The league ID</param>
/// <returns>Insights summary or 404 if no in-progress round</returns>
/// <response code="200">Returns the insights summary</response>
/// <response code="401">User is not authenticated</response>
/// <response code="403">User is not a member of this league</response>
/// <response code="404">League not found or no in-progress round</response>
[HttpGet("{leagueId}/insights")]
[ProducesResponseType(typeof(LeagueInsightsSummary), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<LeagueInsightsSummary>> GetInsights(
    int leagueId,
    CancellationToken cancellationToken)
{
    var query = new GetLeagueInsightsQuery(leagueId);
    var result = await _mediator.Send(query, cancellationToken);

    if (result is null)
    {
        return NotFound();
    }

    return Ok(result);
}

/// <summary>
/// Gets detailed winning scenarios for a specific contender in a league.
/// </summary>
/// <param name="leagueId">The league ID</param>
/// <param name="userId">The contender's user ID</param>
/// <returns>Detailed contender insights including winning scenarios</returns>
/// <response code="200">Returns the contender's detailed scenarios</response>
/// <response code="401">User is not authenticated</response>
/// <response code="403">User is not a member of this league</response>
/// <response code="404">League, contender, or in-progress round not found</response>
[HttpGet("{leagueId}/insights/users/{userId}/scenarios")]
[ProducesResponseType(typeof(ContenderInsights), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<ContenderInsights>> GetContenderScenarios(
    int leagueId,
    string userId,
    CancellationToken cancellationToken)
{
    var query = new GetContenderScenariosQuery(leagueId, userId);
    var result = await _mediator.Send(query, cancellationToken);

    if (result is null)
    {
        return NotFound();
    }

    return Ok(result);
}
```

### Step 2: Add Required Using Statements

```csharp
// Add at top of LeaguesController.cs
using PredictionLeague.Application.Features.Leagues.Queries;
using PredictionLeague.Contracts.Leagues.Insights;
```

### Step 3: Update Swagger Documentation (if needed)

Ensure the Contracts project DTOs have XML documentation for Swagger:

```csharp
// In LeagueInsightsSummary.cs, add XML docs if not present:

/// <summary>
/// Complete insights summary for a league's current in-progress round.
/// </summary>
/// <param name="LeagueId">The league identifier</param>
/// <param name="LeagueName">The league name</param>
/// <param name="RoundId">The current round identifier</param>
/// <param name="RoundNumber">The round number within the season</param>
/// <param name="Month">The month (1-12) for monthly insights</param>
/// <param name="Year">The year for monthly insights</param>
/// <param name="IsLastRoundOfMonth">Whether this is the final round of the month</param>
/// <param name="TotalMatches">Total matches in the round</param>
/// <param name="CompletedMatches">Number of completed matches</param>
/// <param name="LiveMatches">Number of currently live matches</param>
/// <param name="UpcomingMatches">Number of upcoming matches</param>
/// <param name="TotalScenarios">Total number of scenarios calculated</param>
/// <param name="Contenders">Users still in contention with probabilities</param>
/// <param name="EliminatedUsers">Users mathematically eliminated</param>
/// <param name="GeneratedAtUtc">When these insights were calculated</param>
public record LeagueInsightsSummary(
    // ... parameters
);
```

### Step 4: Consider Adding Caching Header

For performance, consider adding cache headers to reduce repeated calculations:

```csharp
[HttpGet("{leagueId}/insights")]
[ResponseCache(Duration = 60, VaryByHeader = "Authorization")]  // Cache for 1 minute
[ProducesResponseType(typeof(LeagueInsightsSummary), StatusCodes.Status200OK)]
// ... rest of method
```

Or use a custom cache attribute:

```csharp
/// <summary>
/// Adds cache headers for insights endpoints.
/// Insights are recalculated when matches complete, so short cache is appropriate.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class InsightsCacheAttribute : ActionFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is OkObjectResult)
        {
            context.HttpContext.Response.Headers["Cache-Control"] = "private, max-age=60";
            context.HttpContext.Response.Headers["Vary"] = "Authorization";
        }
        base.OnResultExecuting(context);
    }
}
```

## Code Patterns to Follow

Follow existing controller patterns in the project:

```csharp
// Example from existing LeaguesController
[HttpGet("{leagueId}")]
[ProducesResponseType(typeof(LeagueDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<LeagueDto>> GetById(
    int leagueId,
    CancellationToken cancellationToken)
{
    var query = new GetLeagueByIdQuery(leagueId);
    var result = await _mediator.Send(query, cancellationToken);

    if (result is null)
    {
        return NotFound();
    }

    return Ok(result);
}
```

Key patterns:
- Use `ActionResult<T>` return type
- Include `CancellationToken` parameter
- Return `NotFound()` for null results
- Include `[ProducesResponseType]` attributes for Swagger
- Use XML documentation for Swagger descriptions

## Verification

- [ ] Endpoints compile without errors
- [ ] GET `/api/leagues/{id}/insights` returns insights for authenticated member
- [ ] GET `/api/leagues/{id}/insights` returns 404 for non-member
- [ ] GET `/api/leagues/{id}/insights` returns 404 when no in-progress round
- [ ] GET `/api/leagues/{id}/insights/users/{userId}/scenarios` returns detailed scenarios
- [ ] Swagger documentation displays correctly
- [ ] Response includes all expected fields

## Edge Cases to Consider

- Requesting insights while logged out (401)
- Requesting insights for a league user isn't in (404 or 403)
- Requesting insights when all matches are complete (404 or empty insights)
- Very large response payload (many contenders, many scenarios)
- Concurrent requests during rapid match updates

## Notes

- Endpoints return 404 rather than 403 for security (don't reveal league existence)
- The handler handles authorization logic, controller just maps null → 404
- Consider rate limiting these endpoints as they're computationally intensive
- Caching should be short-lived as insights change when matches complete

# Task 5: Query and Handler

**Parent Feature:** [Monthly Leaderboard Scenarios](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create MediatR query and handler to expose the scenario calculation logic through the application layer, following CQRS patterns.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Leagues/Queries/GetLeagueInsightsQuery.cs` | Create | Query request definition |
| `PredictionLeague.Application/Features/Leagues/Queries/GetLeagueInsightsQueryHandler.cs` | Create | Handler implementation |
| `PredictionLeague.Application/Features/Leagues/Queries/GetContenderScenariosQuery.cs` | Create | Query for detailed user scenarios |
| `PredictionLeague.Application/Features/Leagues/Queries/GetContenderScenariosQueryHandler.cs` | Create | Handler for detailed scenarios |

## Implementation Steps

### Step 1: Create GetLeagueInsightsQuery

```csharp
// PredictionLeague.Application/Features/Leagues/Queries/GetLeagueInsightsQuery.cs
using MediatR;
using PredictionLeague.Contracts.Leagues.Insights;

namespace PredictionLeague.Application.Features.Leagues.Queries;

/// <summary>
/// Gets insights for a league's current in-progress round,
/// including contention status and win probabilities.
/// </summary>
public record GetLeagueInsightsQuery(int LeagueId) : IRequest<LeagueInsightsSummary?>;
```

### Step 2: Create GetLeagueInsightsQueryHandler

```csharp
// PredictionLeague.Application/Features/Leagues/Queries/GetLeagueInsightsQueryHandler.cs
using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Application.Features.Leagues.Services;
using PredictionLeague.Contracts.Leagues.Insights;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueInsightsQueryHandler
    : IRequestHandler<GetLeagueInsightsQuery, LeagueInsightsSummary?>
{
    private readonly IScenarioCalculator _scenarioCalculator;
    private readonly IApplicationReadDbConnection _readDb;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetLeagueInsightsQueryHandler> _logger;

    public GetLeagueInsightsQueryHandler(
        IScenarioCalculator scenarioCalculator,
        IApplicationReadDbConnection readDb,
        ICurrentUserService currentUserService,
        ILogger<GetLeagueInsightsQueryHandler> logger)
    {
        _scenarioCalculator = scenarioCalculator;
        _readDb = readDb;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<LeagueInsightsSummary?> Handle(
        GetLeagueInsightsQuery request,
        CancellationToken cancellationToken)
    {
        // Verify user is a member of this league
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthenticated request for League (ID: {LeagueId}) insights", request.LeagueId);
            return null;
        }

        var isMember = await IsLeagueMemberAsync(request.LeagueId, userId, cancellationToken);
        if (!isMember)
        {
            _logger.LogWarning(
                "User (ID: {UserId}) is not a member of League (ID: {LeagueId})",
                userId, request.LeagueId);
            return null;
        }

        // Calculate insights
        var insights = await _scenarioCalculator.CalculateInsightsAsync(
            request.LeagueId,
            cancellationToken);

        if (insights is null)
        {
            _logger.LogDebug(
                "No insights available for League (ID: {LeagueId}) - no in-progress round",
                request.LeagueId);
        }
        else
        {
            _logger.LogInformation(
                "Generated insights for League (ID: {LeagueId}), Round (ID: {RoundId}): " +
                "{ContenderCount} contenders, {EliminatedCount} eliminated, {ScenarioCount} scenarios",
                request.LeagueId,
                insights.RoundId,
                insights.Contenders.Count,
                insights.EliminatedUsers.Count,
                insights.TotalScenarios);
        }

        return insights;
    }

    private async Task<bool> IsLeagueMemberAsync(
        int leagueId, string userId, CancellationToken ct)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM [LeagueMembers]
            WHERE [LeagueId] = @LeagueId
              AND [UserId] = @UserId
              AND [Status] = 'Approved'";

        var count = await _readDb.ExecuteScalarAsync<int>(
            sql,
            new { LeagueId = leagueId, UserId = userId },
            ct);

        return count > 0;
    }
}
```

### Step 3: Create GetContenderScenariosQuery

```csharp
// PredictionLeague.Application/Features/Leagues/Queries/GetContenderScenariosQuery.cs
using MediatR;
using PredictionLeague.Contracts.Leagues.Insights;

namespace PredictionLeague.Application.Features.Leagues.Queries;

/// <summary>
/// Gets detailed winning scenarios for a specific contender,
/// including all scenario combinations where they win or tie.
/// </summary>
public record GetContenderScenariosQuery(
    int LeagueId,
    string ContenderUserId
) : IRequest<ContenderInsights?>;
```

### Step 4: Create GetContenderScenariosQueryHandler

```csharp
// PredictionLeague.Application/Features/Leagues/Queries/GetContenderScenariosQueryHandler.cs
using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Application.Features.Leagues.Services;
using PredictionLeague.Contracts.Leagues.Insights;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetContenderScenariosQueryHandler
    : IRequestHandler<GetContenderScenariosQuery, ContenderInsights?>
{
    private readonly IScenarioCalculator _scenarioCalculator;
    private readonly IApplicationReadDbConnection _readDb;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetContenderScenariosQueryHandler> _logger;

    public GetContenderScenariosQueryHandler(
        IScenarioCalculator scenarioCalculator,
        IApplicationReadDbConnection readDb,
        ICurrentUserService currentUserService,
        ILogger<GetContenderScenariosQueryHandler> logger)
    {
        _scenarioCalculator = scenarioCalculator;
        _readDb = readDb;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ContenderInsights?> Handle(
        GetContenderScenariosQuery request,
        CancellationToken cancellationToken)
    {
        // Verify requesting user is a member of this league
        var requestingUserId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(requestingUserId))
        {
            _logger.LogWarning(
                "Unauthenticated request for contender scenarios in League (ID: {LeagueId})",
                request.LeagueId);
            return null;
        }

        var isMember = await IsLeagueMemberAsync(request.LeagueId, requestingUserId, cancellationToken);
        if (!isMember)
        {
            _logger.LogWarning(
                "User (ID: {UserId}) is not a member of League (ID: {LeagueId})",
                requestingUserId, request.LeagueId);
            return null;
        }

        // Verify target contender is also a member
        var isContenderMember = await IsLeagueMemberAsync(
            request.LeagueId, request.ContenderUserId, cancellationToken);
        if (!isContenderMember)
        {
            _logger.LogWarning(
                "Contender (ID: {ContenderUserId}) is not a member of League (ID: {LeagueId})",
                request.ContenderUserId, request.LeagueId);
            return null;
        }

        // Get detailed scenarios for the contender
        var scenarios = await _scenarioCalculator.GetContenderScenariosAsync(
            request.LeagueId,
            request.ContenderUserId,
            cancellationToken);

        if (scenarios is null)
        {
            _logger.LogDebug(
                "No scenarios available for Contender (ID: {ContenderUserId}) in League (ID: {LeagueId})",
                request.ContenderUserId, request.LeagueId);
        }
        else
        {
            _logger.LogInformation(
                "Generated detailed scenarios for Contender (ID: {ContenderUserId}) in League (ID: {LeagueId}): " +
                "{WinScenarios} winning, {TieScenarios} tie scenarios",
                request.ContenderUserId,
                request.LeagueId,
                scenarios.RoundWinScenarioCount,
                scenarios.RoundTieScenarioCount);
        }

        return scenarios;
    }

    private async Task<bool> IsLeagueMemberAsync(
        int leagueId, string userId, CancellationToken ct)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM [LeagueMembers]
            WHERE [LeagueId] = @LeagueId
              AND [UserId] = @UserId
              AND [Status] = 'Approved'";

        var count = await _readDb.ExecuteScalarAsync<int>(
            sql,
            new { LeagueId = leagueId, UserId = userId },
            ct);

        return count > 0;
    }
}
```

### Step 5: Add Validators (Optional but Recommended)

```csharp
// PredictionLeague.Validators/Leagues/GetLeagueInsightsQueryValidator.cs
using FluentValidation;
using PredictionLeague.Application.Features.Leagues.Queries;

namespace PredictionLeague.Validators.Leagues;

public class GetLeagueInsightsQueryValidator : AbstractValidator<GetLeagueInsightsQuery>
{
    public GetLeagueInsightsQueryValidator()
    {
        RuleFor(x => x.LeagueId)
            .GreaterThan(0)
            .WithMessage("League ID must be greater than 0");
    }
}
```

```csharp
// PredictionLeague.Validators/Leagues/GetContenderScenariosQueryValidator.cs
using FluentValidation;
using PredictionLeague.Application.Features.Leagues.Queries;

namespace PredictionLeague.Validators.Leagues;

public class GetContenderScenariosQueryValidator : AbstractValidator<GetContenderScenariosQuery>
{
    public GetContenderScenariosQueryValidator()
    {
        RuleFor(x => x.LeagueId)
            .GreaterThan(0)
            .WithMessage("League ID must be greater than 0");

        RuleFor(x => x.ContenderUserId)
            .NotEmpty()
            .WithMessage("Contender user ID is required");
    }
}
```

## Code Patterns to Follow

Follow existing query handler patterns:

```csharp
// Example from existing codebase - GetMyLeaguesQueryHandler
public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    private readonly IApplicationReadDbConnection _readDb;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetMyLeaguesQueryHandler> _logger;

    public GetMyLeaguesQueryHandler(
        IApplicationReadDbConnection readDb,
        ICurrentUserService currentUserService,
        ILogger<GetMyLeaguesQueryHandler> logger)
    {
        _readDb = readDb;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<IEnumerable<MyLeagueDto>> Handle(
        GetMyLeaguesQuery request,
        CancellationToken cancellationToken)
    {
        // Implementation...
    }
}
```

Key patterns:
- Use `IApplicationReadDbConnection` for data access (not repositories)
- Inject `ICurrentUserService` to get authenticated user
- Log with entity ID format: "Entity (ID: {EntityId})"
- Return nullable types when data may not exist

## Verification

- [ ] Queries compile without errors
- [ ] Handlers correctly check league membership before returning data
- [ ] Unauthenticated requests return null (not throw)
- [ ] Requests for non-existent leagues return null
- [ ] Validators reject invalid input (negative IDs, empty user IDs)
- [ ] Logging follows entity ID format conventions

## Edge Cases to Consider

- User requests insights for a league they're not a member of
- User requests scenarios for a contender who isn't in the league
- League has no in-progress round (return null, not error)
- User's authentication token expired during request
- Very slow calculation (consider timeout handling)

## Notes

- Handlers return null for authorization failures rather than throwing exceptions
- This follows the pattern of graceful degradation in the UI
- The API layer can convert null to 404 if needed
- Both handlers reuse the `IsLeagueMemberAsync` check - could be extracted to a shared service if used elsewhere

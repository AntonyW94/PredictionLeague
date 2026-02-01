# Task 1: Data Models

**Parent Feature:** [Monthly Leaderboard Scenarios](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create all DTOs needed to represent insights, scenarios, constraints, and eliminated users for the scenario calculation feature.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Contracts/Leagues/Insights/LeagueInsightsSummary.cs` | Create | Main response DTO for insights endpoint |
| `PredictionLeague.Contracts/Leagues/Insights/ContenderInsights.cs` | Create | Per-user insights with probabilities |
| `PredictionLeague.Contracts/Leagues/Insights/EliminatedUserDto.cs` | Create | Info about eliminated users |
| `PredictionLeague.Contracts/Leagues/Insights/WinningScenario.cs` | Create | Single scenario where user wins |
| `PredictionLeague.Contracts/Leagues/Insights/ScenarioMatchResult.cs` | Create | Match result within a scenario |
| `PredictionLeague.Contracts/Leagues/Insights/MatchConstraint.cs` | Create | What a user needs for a specific match |
| `PredictionLeague.Contracts/Leagues/Insights/Scoreline.cs` | Create | A specific score with reason |
| `PredictionLeague.Contracts/Leagues/Insights/ResultType.cs` | Create | Enum for home win/draw/away win |

## Implementation Steps

### Step 1: Create ResultType Enum

```csharp
// PredictionLeague.Contracts/Leagues/Insights/ResultType.cs
namespace PredictionLeague.Contracts.Leagues.Insights;

public enum ResultType
{
    HomeWin,
    Draw,
    AwayWin
}
```

### Step 2: Create Scoreline Record

```csharp
// PredictionLeague.Contracts/Leagues/Insights/Scoreline.cs
namespace PredictionLeague.Contracts.Leagues.Insights;

/// <summary>
/// Represents a specific match scoreline, optionally with a reason
/// (e.g., "John's prediction").
/// </summary>
public record Scoreline(
    int HomeScore,
    int AwayScore,
    string? Reason = null
)
{
    public ResultType ResultType => HomeScore > AwayScore
        ? ResultType.HomeWin
        : HomeScore < AwayScore
            ? ResultType.AwayWin
            : ResultType.Draw;

    public override string ToString() => $"{HomeScore}-{AwayScore}";
}
```

### Step 3: Create ScenarioMatchResult Record

```csharp
// PredictionLeague.Contracts/Leagues/Insights/ScenarioMatchResult.cs
namespace PredictionLeague.Contracts.Leagues.Insights;

/// <summary>
/// Represents a hypothetical match result within a scenario.
/// </summary>
public record ScenarioMatchResult(
    int MatchId,
    string HomeTeam,
    string AwayTeam,
    int HomeScore,
    int AwayScore,
    bool IsGenericResult  // True if this represents "any other home win" etc.
)
{
    public ResultType ResultType => HomeScore > AwayScore
        ? ResultType.HomeWin
        : HomeScore < AwayScore
            ? ResultType.AwayWin
            : ResultType.Draw;
}
```

### Step 4: Create WinningScenario Record

```csharp
// PredictionLeague.Contracts/Leagues/Insights/WinningScenario.cs
namespace PredictionLeague.Contracts.Leagues.Insights;

/// <summary>
/// Represents a single scenario (combination of match results)
/// where a user wins or ties.
/// </summary>
public record WinningScenario(
    int ScenarioId,
    IReadOnlyList<ScenarioMatchResult> MatchResults,
    bool WinsRoundOutright,
    bool TiesForRound,
    bool WinsMonthOutright,
    bool TiesForMonth,
    int FinalRoundPoints,
    int FinalMonthlyPoints
);
```

### Step 5: Create MatchConstraint Record

```csharp
// PredictionLeague.Contracts/Leagues/Insights/MatchConstraint.cs
namespace PredictionLeague.Contracts.Leagues.Insights;

/// <summary>
/// Describes what result(s) a user needs for a specific match to have a chance of winning.
/// </summary>
public record MatchConstraint(
    int MatchId,
    string HomeTeam,
    string AwayTeam,

    /// <summary>
    /// The result type(s) needed. Null means any result works.
    /// </summary>
    IReadOnlyList<ResultType>? RequiredResultTypes,

    /// <summary>
    /// True if any result works for this match.
    /// </summary>
    bool AnyResultWorks,

    /// <summary>
    /// Specific scorelines that would NOT result in a win (within the required result type).
    /// </summary>
    IReadOnlyList<Scoreline> ExcludedScorelines,

    /// <summary>
    /// If only specific scorelines work (rare), list them here.
    /// </summary>
    IReadOnlyList<Scoreline>? RequiredScorelines
)
{
    /// <summary>
    /// Generates a human-readable description of the constraint.
    /// </summary>
    public string GetDescription()
    {
        if (AnyResultWorks)
            return "Any result works";

        if (RequiredScorelines is { Count: > 0 })
            return $"Need exactly: {string.Join(" or ", RequiredScorelines)}";

        var resultDesc = RequiredResultTypes switch
        {
            { Count: 1 } => RequiredResultTypes[0] switch
            {
                ResultType.HomeWin => "Home win",
                ResultType.Draw => "Draw",
                ResultType.AwayWin => "Away win",
                _ => "Unknown"
            },
            { Count: 2 } => string.Join(" or ", RequiredResultTypes.Select(r => r switch
            {
                ResultType.HomeWin => "home win",
                ResultType.Draw => "draw",
                ResultType.AwayWin => "away win",
                _ => "unknown"
            })),
            _ => "Any result"
        };

        if (ExcludedScorelines.Count > 0)
        {
            var excluded = string.Join(", ", ExcludedScorelines.Select(s => s.ToString()));
            return $"{resultDesc} (but NOT {excluded})";
        }

        return resultDesc;
    }
}
```

### Step 6: Create EliminatedUserDto Record

```csharp
// PredictionLeague.Contracts/Leagues/Insights/EliminatedUserDto.cs
namespace PredictionLeague.Contracts.Leagues.Insights;

/// <summary>
/// Information about a user who has been mathematically eliminated.
/// </summary>
public record EliminatedUserDto(
    string UserId,
    string UserName,
    int CurrentRoundPoints,
    int CurrentMonthlyPoints,
    int MaxPossibleRoundPoints,
    int MaxPossibleMonthlyPoints,

    /// <summary>
    /// The user who would beat them even in their best-case scenario.
    /// </summary>
    string EliminatedByUserId,
    string EliminatedByUserName
);
```

### Step 7: Create ContenderInsights Record

```csharp
// PredictionLeague.Contracts/Leagues/Insights/ContenderInsights.cs
namespace PredictionLeague.Contracts.Leagues.Insights;

/// <summary>
/// Detailed insights for a single user who is still in contention.
/// </summary>
public record ContenderInsights(
    string UserId,
    string UserName,
    int CurrentRoundPoints,
    int CurrentMonthlyPoints,
    bool HasBoostApplied,

    // Round probabilities
    decimal RoundWinProbability,
    decimal RoundTieProbability,
    int RoundWinScenarioCount,
    int RoundTieScenarioCount,

    // Monthly probabilities (only populated for last round of month)
    decimal? MonthlyWinProbability,
    decimal? MonthlyTieProbability,
    int? MonthlyWinScenarioCount,
    int? MonthlyTieScenarioCount,

    // Combined probability (win both round AND month)
    decimal? BothWinProbability,

    /// <summary>
    /// What this user needs for each remaining match.
    /// </summary>
    IReadOnlyList<MatchConstraint> MatchConstraints,

    /// <summary>
    /// All scenarios where this user wins or ties.
    /// May be null if not requested (to reduce payload size).
    /// </summary>
    IReadOnlyList<WinningScenario>? WinningScenarios
);
```

### Step 8: Create LeagueInsightsSummary Record

```csharp
// PredictionLeague.Contracts/Leagues/Insights/LeagueInsightsSummary.cs
namespace PredictionLeague.Contracts.Leagues.Insights;

/// <summary>
/// Complete insights summary for a league's current in-progress round.
/// </summary>
public record LeagueInsightsSummary(
    int LeagueId,
    string LeagueName,

    // Round info
    int RoundId,
    int RoundNumber,

    // Month info
    int Month,
    int Year,
    bool IsLastRoundOfMonth,

    // Match status
    int TotalMatches,
    int CompletedMatches,
    int LiveMatches,
    int UpcomingMatches,

    // Scenario stats
    int TotalScenarios,

    /// <summary>
    /// Users still in contention with their probabilities.
    /// Ordered by win probability descending.
    /// </summary>
    IReadOnlyList<ContenderInsights> Contenders,

    /// <summary>
    /// Users who have been mathematically eliminated.
    /// </summary>
    IReadOnlyList<EliminatedUserDto> EliminatedUsers,

    /// <summary>
    /// When these insights were calculated.
    /// </summary>
    DateTime GeneratedAtUtc
);
```

## Code Patterns to Follow

Follow existing DTO patterns in the Contracts project:

```csharp
// Example from existing code - LeagueDto.cs
namespace PredictionLeague.Contracts.Leagues;

public record LeagueDto(
    int Id,
    string Name,
    // ... properties
);
```

Key patterns:
- Use `record` types for immutability
- Use `IReadOnlyList<T>` for collections
- Nullable reference types for optional data
- XML documentation comments for public APIs

## Verification

- [ ] All DTOs compile without errors
- [ ] Records have appropriate nullability annotations
- [ ] `MatchConstraint.GetDescription()` produces readable output
- [ ] `Scoreline.ResultType` correctly identifies home win/draw/away win
- [ ] Collections use `IReadOnlyList<T>` not `List<T>`

## Edge Cases to Consider

- Empty lists (no contenders, no eliminated users, no constraints)
- Null monthly data when not the last round of month
- Very long user names in display
- Zero scenarios (all matches complete)

## Notes

- DTOs are intentionally comprehensive - UI can choose what to display
- `WinningScenarios` on `ContenderInsights` is nullable to allow fetching summary without full scenario data
- `IsGenericResult` flag on `ScenarioMatchResult` distinguishes "Liverpool 2-1" from "Any Liverpool home win"

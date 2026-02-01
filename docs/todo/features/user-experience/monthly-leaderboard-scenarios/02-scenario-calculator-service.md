# Task 2: Scenario Calculator Service

**Parent Feature:** [Monthly Leaderboard Scenarios](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create the core `ScenarioCalculator` service that performs elimination checks and enumerates all possible scenarios to calculate win/tie probabilities.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Leagues/Services/IScenarioCalculator.cs` | Create | Interface for scenario calculation |
| `PredictionLeague.Application/Features/Leagues/Services/ScenarioCalculator.cs` | Create | Main implementation |
| `PredictionLeague.Application/Features/Leagues/Services/ScenarioModels.cs` | Create | Internal models for calculation |
| `PredictionLeague.Application/DependencyInjection.cs` | Modify | Register service |

## Implementation Steps

### Step 1: Create Internal Models for Calculation

These models are internal to the service, not exposed via API.

```csharp
// PredictionLeague.Application/Features/Leagues/Services/ScenarioModels.cs
namespace PredictionLeague.Application.Features.Leagues.Services;

/// <summary>
/// Internal model representing a league member during calculation.
/// </summary>
internal class MemberCalculation
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required int CurrentRoundPoints { get; init; }
    public required int PreviousMonthlyPoints { get; init; }  // Points from earlier rounds in month
    public required bool HasBoost { get; init; }
    public required Dictionary<int, Prediction> Predictions { get; init; }  // MatchId -> Prediction

    // Calculated during scenarios
    public int ScenarioRoundPoints { get; set; }
    public int ScenarioMonthlyPoints { get; set; }
}

internal record Prediction(int HomeScore, int AwayScore);

/// <summary>
/// Represents a possible outcome for a match.
/// </summary>
internal abstract record MatchOutcome(int MatchId);

/// <summary>
/// A specific scoreline outcome (someone's prediction or current live score).
/// </summary>
internal record ExactScoreOutcome(int MatchId, int HomeScore, int AwayScore) : MatchOutcome(MatchId);

/// <summary>
/// A generic result type (home win/draw/away win that doesn't match any exact prediction).
/// </summary>
internal record GenericResultOutcome(int MatchId, ResultType ResultType) : MatchOutcome(MatchId);

/// <summary>
/// Represents a remaining match with its possible outcomes.
/// </summary>
internal class RemainingMatch
{
    public required int MatchId { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public required bool IsLive { get; init; }
    public int? LiveHomeScore { get; init; }
    public int? LiveAwayScore { get; init; }
    public required List<MatchOutcome> PossibleOutcomes { get; init; }
}

/// <summary>
/// Results of a single scenario calculation.
/// </summary>
internal class ScenarioResult
{
    public required int ScenarioId { get; init; }
    public required List<MatchOutcome> Outcomes { get; init; }
    public required List<string> RoundWinnerIds { get; init; }
    public required List<string> MonthlyWinnerIds { get; init; }
    public required Dictionary<string, int> FinalRoundPoints { get; init; }
    public required Dictionary<string, int> FinalMonthlyPoints { get; init; }
}
```

### Step 2: Create Interface

```csharp
// PredictionLeague.Application/Features/Leagues/Services/IScenarioCalculator.cs
using PredictionLeague.Contracts.Leagues.Insights;

namespace PredictionLeague.Application.Features.Leagues.Services;

public interface IScenarioCalculator
{
    /// <summary>
    /// Calculates full insights for a league's current in-progress round.
    /// </summary>
    Task<LeagueInsightsSummary?> CalculateInsightsAsync(
        int leagueId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed scenarios for a specific user.
    /// Includes the full list of winning scenarios.
    /// </summary>
    Task<ContenderInsights?> GetContenderScenariosAsync(
        int leagueId,
        string userId,
        CancellationToken cancellationToken = default);
}
```

### Step 3: Create Main Service Implementation

```csharp
// PredictionLeague.Application/Features/Leagues/Services/ScenarioCalculator.cs
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Leagues.Insights;

namespace PredictionLeague.Application.Features.Leagues.Services;

public class ScenarioCalculator : IScenarioCalculator
{
    private readonly IApplicationReadDbConnection _readDb;
    private readonly ILogger<ScenarioCalculator> _logger;

    public ScenarioCalculator(
        IApplicationReadDbConnection readDb,
        ILogger<ScenarioCalculator> logger)
    {
        _readDb = readDb;
        _logger = logger;
    }

    public async Task<LeagueInsightsSummary?> CalculateInsightsAsync(
        int leagueId,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Get the current in-progress round
        var roundInfo = await GetInProgressRoundAsync(leagueId, cancellationToken);
        if (roundInfo is null)
        {
            _logger.LogDebug("No in-progress round found for League (ID: {LeagueId})", leagueId);
            return null;
        }

        // Step 2: Gather all required data
        var data = await GatherCalculationDataAsync(leagueId, roundInfo, cancellationToken);

        // Step 3: Check if there are remaining matches
        if (data.RemainingMatches.Count == 0)
        {
            _logger.LogDebug("No remaining matches in Round (ID: {RoundId})", roundInfo.RoundId);
            return CreateCompletedRoundSummary(leagueId, roundInfo, data);
        }

        // Step 4: Perform elimination check
        var (contenders, eliminated) = PerformEliminationCheck(data);

        // Step 5: Build outcome space for remaining matches
        BuildOutcomeSpace(data.RemainingMatches, contenders);

        // Step 6: Enumerate all scenarios
        var scenarioResults = EnumerateScenarios(data.RemainingMatches, data.Members, roundInfo.IsLastRoundOfMonth);

        // Step 7: Aggregate results into probabilities
        var contenderInsights = AggregateResults(contenders, scenarioResults, data.RemainingMatches, roundInfo.IsLastRoundOfMonth);

        // Step 8: Build summary
        return BuildSummary(leagueId, roundInfo, data, contenderInsights, eliminated, scenarioResults.Count);
    }

    public async Task<ContenderInsights?> GetContenderScenariosAsync(
        int leagueId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Similar to CalculateInsightsAsync but returns full WinningScenarios for one user
        // Implementation follows same pattern but includes scenario details
        throw new NotImplementedException();
    }

    #region Data Gathering

    private async Task<RoundInfo?> GetInProgressRoundAsync(int leagueId, CancellationToken ct)
    {
        const string sql = @"
            SELECT
                r.[Id] AS RoundId,
                r.[RoundNumber],
                r.[StartDateUtc],
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                l.[PointsForExactScore],
                l.[PointsForCorrectResult],
                s.[Id] AS SeasonId
            FROM [Rounds] r
            INNER JOIN [Seasons] s ON r.[SeasonId] = s.[Id]
            INNER JOIN [Leagues] l ON l.[SeasonId] = s.[Id]
            WHERE l.[Id] = @LeagueId
              AND r.[Status] = 'InProgress'";

        var result = await _readDb.QuerySingleOrDefaultAsync<RoundInfo>(sql, new { LeagueId = leagueId }, ct);

        if (result is not null)
        {
            // Determine if last round of month
            result = result with { IsLastRoundOfMonth = await CheckIsLastRoundOfMonthAsync(result, ct) };
        }

        return result;
    }

    private async Task<bool> CheckIsLastRoundOfMonthAsync(RoundInfo roundInfo, CancellationToken ct)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM [Rounds] r
            WHERE r.[SeasonId] = @SeasonId
              AND MONTH(r.[StartDateUtc]) = @Month
              AND YEAR(r.[StartDateUtc]) = @Year
              AND r.[RoundNumber] > @RoundNumber
              AND r.[Status] != 'Draft'";

        var month = roundInfo.StartDateUtc.Month;
        var year = roundInfo.StartDateUtc.Year;

        var laterRoundsInMonth = await _readDb.ExecuteScalarAsync<int>(sql, new
        {
            roundInfo.SeasonId,
            Month = month,
            Year = year,
            roundInfo.RoundNumber
        }, ct);

        return laterRoundsInMonth == 0;
    }

    private async Task<CalculationData> GatherCalculationDataAsync(
        int leagueId, RoundInfo roundInfo, CancellationToken ct)
    {
        // Gather in parallel where possible
        var membersTask = GetLeagueMembersWithPointsAsync(leagueId, roundInfo, ct);
        var matchesTask = GetMatchesAsync(roundInfo.RoundId, ct);
        var predictionsTask = GetPredictionsAsync(leagueId, roundInfo.RoundId, ct);
        var boostsTask = GetBoostsAsync(leagueId, roundInfo.RoundId, ct);

        await Task.WhenAll(membersTask, matchesTask, predictionsTask, boostsTask);

        var members = await membersTask;
        var matches = await matchesTask;
        var predictions = await predictionsTask;
        var boosts = await boostsTask;

        // Categorise matches
        var completedMatches = matches.Where(m => m.Status == "Completed").ToList();
        var liveMatches = matches.Where(m => m.Status == "InProgress").ToList();
        var upcomingMatches = matches.Where(m => m.Status == "Scheduled").ToList();

        // Build remaining matches with metadata
        var remainingMatches = liveMatches.Concat(upcomingMatches)
            .Select(m => new RemainingMatch
            {
                MatchId = m.MatchId,
                HomeTeam = m.HomeTeam,
                AwayTeam = m.AwayTeam,
                IsLive = m.Status == "InProgress",
                LiveHomeScore = m.LiveHomeScore,
                LiveAwayScore = m.LiveAwayScore,
                PossibleOutcomes = new List<MatchOutcome>()
            })
            .ToList();

        // Build member calculations
        var memberCalculations = members.Select(m => new MemberCalculation
        {
            UserId = m.UserId,
            UserName = m.UserName,
            CurrentRoundPoints = m.CurrentRoundPoints,
            PreviousMonthlyPoints = m.PreviousMonthlyPoints,
            HasBoost = boosts.Contains(m.UserId),
            Predictions = predictions
                .Where(p => p.UserId == m.UserId)
                .ToDictionary(p => p.MatchId, p => new Prediction(p.HomeScore, p.AwayScore))
        }).ToList();

        return new CalculationData
        {
            RoundInfo = roundInfo,
            Members = memberCalculations,
            CompletedMatches = completedMatches.Count,
            LiveMatches = liveMatches.Count,
            UpcomingMatches = upcomingMatches.Count,
            RemainingMatches = remainingMatches
        };
    }

    // Additional data gathering methods...
    // GetLeagueMembersWithPointsAsync, GetMatchesAsync, GetPredictionsAsync, GetBoostsAsync

    #endregion

    #region Elimination Check

    private (List<MemberCalculation> Contenders, List<EliminatedUserDto> Eliminated) PerformEliminationCheck(
        CalculationData data)
    {
        var contenders = new List<MemberCalculation>();
        var eliminated = new List<EliminatedUserDto>();

        foreach (var member in data.Members)
        {
            // Simulate member's best case: all their predictions are correct
            var hypotheticalPoints = SimulateBestCase(member, data);

            // Check if member finishes 1st or tied in this scenario
            var maxPoints = hypotheticalPoints.Values.Max();
            var memberPoints = hypotheticalPoints[member.UserId];

            if (memberPoints >= maxPoints)
            {
                contenders.Add(member);
            }
            else
            {
                // Find who beat them
                var beatenBy = hypotheticalPoints
                    .Where(kvp => kvp.Value == maxPoints)
                    .Select(kvp => data.Members.First(m => m.UserId == kvp.Key))
                    .First();

                eliminated.Add(new EliminatedUserDto(
                    UserId: member.UserId,
                    UserName: member.UserName,
                    CurrentRoundPoints: member.CurrentRoundPoints,
                    CurrentMonthlyPoints: member.CurrentRoundPoints + member.PreviousMonthlyPoints,
                    MaxPossibleRoundPoints: memberPoints,
                    MaxPossibleMonthlyPoints: memberPoints + member.PreviousMonthlyPoints,
                    EliminatedByUserId: beatenBy.UserId,
                    EliminatedByUserName: beatenBy.UserName
                ));
            }
        }

        _logger.LogDebug(
            "Elimination check complete: {ContenderCount} contenders, {EliminatedCount} eliminated",
            contenders.Count, eliminated.Count);

        return (contenders, eliminated);
    }

    private Dictionary<string, int> SimulateBestCase(MemberCalculation member, CalculationData data)
    {
        // For each remaining match, assume member's prediction is the actual result
        // Calculate what everyone would score

        var results = new Dictionary<string, int>();

        foreach (var m in data.Members)
        {
            var points = m.CurrentRoundPoints;

            foreach (var match in data.RemainingMatches)
            {
                // The "actual result" is the member-under-test's prediction
                if (!member.Predictions.TryGetValue(match.MatchId, out var actualResult))
                    continue;  // Member didn't predict this match

                // Calculate points for user m given this result
                if (m.Predictions.TryGetValue(match.MatchId, out var mPrediction))
                {
                    var matchPoints = CalculatePoints(
                        mPrediction,
                        actualResult,
                        data.RoundInfo.PointsForExactScore,
                        data.RoundInfo.PointsForCorrectResult,
                        match.IsLive ? match.LiveHomeScore : null,
                        match.IsLive ? match.LiveAwayScore : null);

                    if (m.HasBoost)
                        matchPoints *= 2;

                    points += matchPoints;
                }
            }

            results[m.UserId] = points;
        }

        return results;
    }

    #endregion

    #region Outcome Space

    private void BuildOutcomeSpace(List<RemainingMatch> remainingMatches, List<MemberCalculation> contenders)
    {
        foreach (var match in remainingMatches)
        {
            // Get unique predictions from contenders for this match
            var uniquePredictions = contenders
                .Where(c => c.Predictions.ContainsKey(match.MatchId))
                .Select(c => c.Predictions[match.MatchId])
                .Distinct()
                .ToList();

            // Filter by live match constraints if applicable
            if (match.IsLive)
            {
                uniquePredictions = uniquePredictions
                    .Where(p => p.HomeScore >= match.LiveHomeScore!.Value
                             && p.AwayScore >= match.LiveAwayScore!.Value)
                    .ToList();
            }

            // Add each unique prediction as a possible outcome
            foreach (var pred in uniquePredictions)
            {
                match.PossibleOutcomes.Add(new ExactScoreOutcome(match.MatchId, pred.HomeScore, pred.AwayScore));
            }

            // Add generic result types
            match.PossibleOutcomes.Add(new GenericResultOutcome(match.MatchId, ResultType.HomeWin));
            match.PossibleOutcomes.Add(new GenericResultOutcome(match.MatchId, ResultType.Draw));
            match.PossibleOutcomes.Add(new GenericResultOutcome(match.MatchId, ResultType.AwayWin));

            _logger.LogDebug(
                "Match (ID: {MatchId}): {OutcomeCount} possible outcomes",
                match.MatchId, match.PossibleOutcomes.Count);
        }
    }

    #endregion

    #region Scenario Enumeration

    private List<ScenarioResult> EnumerateScenarios(
        List<RemainingMatch> remainingMatches,
        List<MemberCalculation> allMembers,
        bool isLastRoundOfMonth)
    {
        var results = new List<ScenarioResult>();

        // Calculate total scenario count
        var totalScenarios = remainingMatches
            .Select(m => m.PossibleOutcomes.Count)
            .Aggregate(1, (a, b) => a * b);

        _logger.LogInformation("Enumerating {ScenarioCount} scenarios", totalScenarios);

        // Generate all combinations using iterative approach
        var currentIndices = new int[remainingMatches.Count];
        var scenarioId = 0;

        do
        {
            // Build current scenario
            var outcomes = new List<MatchOutcome>();
            for (var i = 0; i < remainingMatches.Count; i++)
            {
                outcomes.Add(remainingMatches[i].PossibleOutcomes[currentIndices[i]]);
            }

            // Calculate all members' points for this scenario
            var scenarioResult = CalculateScenarioResult(scenarioId++, outcomes, remainingMatches, allMembers, isLastRoundOfMonth);
            results.Add(scenarioResult);

        } while (IncrementIndices(currentIndices, remainingMatches));

        return results;
    }

    private bool IncrementIndices(int[] indices, List<RemainingMatch> matches)
    {
        for (var i = indices.Length - 1; i >= 0; i--)
        {
            indices[i]++;
            if (indices[i] < matches[i].PossibleOutcomes.Count)
                return true;
            indices[i] = 0;
        }
        return false;
    }

    private ScenarioResult CalculateScenarioResult(
        int scenarioId,
        List<MatchOutcome> outcomes,
        List<RemainingMatch> matches,
        List<MemberCalculation> allMembers,
        bool isLastRoundOfMonth)
    {
        var finalRoundPoints = new Dictionary<string, int>();
        var finalMonthlyPoints = new Dictionary<string, int>();

        foreach (var member in allMembers)
        {
            var roundPoints = member.CurrentRoundPoints;

            for (var i = 0; i < outcomes.Count; i++)
            {
                var outcome = outcomes[i];
                var match = matches[i];

                if (!member.Predictions.TryGetValue(match.MatchId, out var prediction))
                    continue;

                var points = CalculatePointsForOutcome(prediction, outcome, match);

                if (member.HasBoost)
                    points *= 2;

                roundPoints += points;
            }

            finalRoundPoints[member.UserId] = roundPoints;
            finalMonthlyPoints[member.UserId] = roundPoints + member.PreviousMonthlyPoints;
        }

        // Determine winners
        var maxRoundPoints = finalRoundPoints.Values.Max();
        var roundWinners = finalRoundPoints
            .Where(kvp => kvp.Value == maxRoundPoints)
            .Select(kvp => kvp.Key)
            .ToList();

        var monthlyWinners = new List<string>();
        if (isLastRoundOfMonth)
        {
            var maxMonthlyPoints = finalMonthlyPoints.Values.Max();
            monthlyWinners = finalMonthlyPoints
                .Where(kvp => kvp.Value == maxMonthlyPoints)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        return new ScenarioResult
        {
            ScenarioId = scenarioId,
            Outcomes = outcomes,
            RoundWinnerIds = roundWinners,
            MonthlyWinnerIds = monthlyWinners,
            FinalRoundPoints = finalRoundPoints,
            FinalMonthlyPoints = finalMonthlyPoints
        };
    }

    #endregion

    #region Points Calculation

    private int CalculatePointsForOutcome(
        Prediction prediction,
        MatchOutcome outcome,
        RemainingMatch match)
    {
        // Use placeholder values - actual values come from league settings
        const int exactPoints = 5;
        const int correctResultPoints = 3;

        return outcome switch
        {
            ExactScoreOutcome exact =>
                IsExactMatch(prediction, exact, match)
                    ? exactPoints
                    : IsSameResultType(prediction, exact)
                        ? correctResultPoints
                        : 0,

            GenericResultOutcome generic =>
                GetResultType(prediction) == generic.ResultType
                    ? correctResultPoints
                    : 0,

            _ => 0
        };
    }

    private bool IsExactMatch(Prediction prediction, ExactScoreOutcome outcome, RemainingMatch match)
    {
        // If match is live, check if prediction is still achievable
        if (match.IsLive)
        {
            if (prediction.HomeScore < match.LiveHomeScore!.Value ||
                prediction.AwayScore < match.LiveAwayScore!.Value)
            {
                return false;  // Exact score no longer possible
            }
        }

        return prediction.HomeScore == outcome.HomeScore &&
               prediction.AwayScore == outcome.AwayScore;
    }

    private bool IsSameResultType(Prediction prediction, ExactScoreOutcome outcome)
    {
        return GetResultType(prediction) == GetResultType(outcome.HomeScore, outcome.AwayScore);
    }

    private ResultType GetResultType(Prediction p) => GetResultType(p.HomeScore, p.AwayScore);

    private ResultType GetResultType(int home, int away)
    {
        if (home > away) return ResultType.HomeWin;
        if (home < away) return ResultType.AwayWin;
        return ResultType.Draw;
    }

    private int CalculatePoints(
        Prediction prediction,
        Prediction actualResult,
        int exactPoints,
        int correctResultPoints,
        int? liveHomeScore,
        int? liveAwayScore)
    {
        // Check if exact score is still possible for live matches
        if (liveHomeScore.HasValue && liveAwayScore.HasValue)
        {
            if (prediction.HomeScore < liveHomeScore.Value ||
                prediction.AwayScore < liveAwayScore.Value)
            {
                // Exact score impossible, can only get correct result
                return GetResultType(prediction) == GetResultType(actualResult)
                    ? correctResultPoints
                    : 0;
            }
        }

        // Exact match?
        if (prediction.HomeScore == actualResult.HomeScore &&
            prediction.AwayScore == actualResult.AwayScore)
        {
            return exactPoints;
        }

        // Same result type?
        if (GetResultType(prediction) == GetResultType(actualResult))
        {
            return correctResultPoints;
        }

        return 0;
    }

    #endregion

    #region Result Aggregation

    private List<ContenderInsights> AggregateResults(
        List<MemberCalculation> contenders,
        List<ScenarioResult> scenarios,
        List<RemainingMatch> remainingMatches,
        bool isLastRoundOfMonth)
    {
        var totalScenarios = scenarios.Count;

        return contenders.Select(c =>
        {
            var roundWins = scenarios.Count(s =>
                s.RoundWinnerIds.Contains(c.UserId) && s.RoundWinnerIds.Count == 1);
            var roundTies = scenarios.Count(s =>
                s.RoundWinnerIds.Contains(c.UserId) && s.RoundWinnerIds.Count > 1);

            var monthlyWins = isLastRoundOfMonth
                ? scenarios.Count(s => s.MonthlyWinnerIds.Contains(c.UserId) && s.MonthlyWinnerIds.Count == 1)
                : (int?)null;
            var monthlyTies = isLastRoundOfMonth
                ? scenarios.Count(s => s.MonthlyWinnerIds.Contains(c.UserId) && s.MonthlyWinnerIds.Count > 1)
                : (int?)null;

            var bothWins = isLastRoundOfMonth
                ? scenarios.Count(s =>
                    s.RoundWinnerIds.Contains(c.UserId) &&
                    s.MonthlyWinnerIds.Contains(c.UserId))
                : (int?)null;

            // Constraint analysis done in separate task
            var constraints = new List<MatchConstraint>();

            return new ContenderInsights(
                UserId: c.UserId,
                UserName: c.UserName,
                CurrentRoundPoints: c.CurrentRoundPoints,
                CurrentMonthlyPoints: c.CurrentRoundPoints + c.PreviousMonthlyPoints,
                HasBoostApplied: c.HasBoost,
                RoundWinProbability: Math.Round((decimal)roundWins / totalScenarios * 100, 2),
                RoundTieProbability: Math.Round((decimal)roundTies / totalScenarios * 100, 2),
                RoundWinScenarioCount: roundWins,
                RoundTieScenarioCount: roundTies,
                MonthlyWinProbability: monthlyWins.HasValue
                    ? Math.Round((decimal)monthlyWins.Value / totalScenarios * 100, 2)
                    : null,
                MonthlyTieProbability: monthlyTies.HasValue
                    ? Math.Round((decimal)monthlyTies.Value / totalScenarios * 100, 2)
                    : null,
                MonthlyWinScenarioCount: monthlyWins,
                MonthlyTieScenarioCount: monthlyTies,
                BothWinProbability: bothWins.HasValue
                    ? Math.Round((decimal)bothWins.Value / totalScenarios * 100, 2)
                    : null,
                MatchConstraints: constraints,
                WinningScenarios: null  // Not included in summary
            );
        })
        .OrderByDescending(c => c.RoundWinProbability)
        .ThenByDescending(c => c.RoundTieProbability)
        .ToList();
    }

    #endregion

    #region Summary Building

    private LeagueInsightsSummary BuildSummary(
        int leagueId,
        RoundInfo roundInfo,
        CalculationData data,
        List<ContenderInsights> contenders,
        List<EliminatedUserDto> eliminated,
        int totalScenarios)
    {
        return new LeagueInsightsSummary(
            LeagueId: leagueId,
            LeagueName: roundInfo.LeagueName,
            RoundId: roundInfo.RoundId,
            RoundNumber: roundInfo.RoundNumber,
            Month: roundInfo.StartDateUtc.Month,
            Year: roundInfo.StartDateUtc.Year,
            IsLastRoundOfMonth: roundInfo.IsLastRoundOfMonth,
            TotalMatches: data.CompletedMatches + data.LiveMatches + data.UpcomingMatches,
            CompletedMatches: data.CompletedMatches,
            LiveMatches: data.LiveMatches,
            UpcomingMatches: data.UpcomingMatches,
            TotalScenarios: totalScenarios,
            Contenders: contenders,
            EliminatedUsers: eliminated,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    private LeagueInsightsSummary? CreateCompletedRoundSummary(
        int leagueId, RoundInfo roundInfo, CalculationData data)
    {
        // All matches complete - return final standings, no scenarios
        // Implementation depends on whether we want to show anything in this case
        return null;
    }

    #endregion
}

// Supporting record types for data queries
internal record RoundInfo(
    int RoundId,
    int RoundNumber,
    DateTime StartDateUtc,
    int LeagueId,
    string LeagueName,
    int PointsForExactScore,
    int PointsForCorrectResult,
    int SeasonId,
    bool IsLastRoundOfMonth = false
);

internal class CalculationData
{
    public required RoundInfo RoundInfo { get; init; }
    public required List<MemberCalculation> Members { get; init; }
    public required int CompletedMatches { get; init; }
    public required int LiveMatches { get; init; }
    public required int UpcomingMatches { get; init; }
    public required List<RemainingMatch> RemainingMatches { get; init; }
}
```

### Step 4: Register Service in DI

```csharp
// In DependencyInjection.cs, add:
services.AddScoped<IScenarioCalculator, ScenarioCalculator>();
```

## Code Patterns to Follow

Follow existing service patterns in the Application layer:

```csharp
// Example from existing code - services use constructor injection
public class SomeService : ISomeService
{
    private readonly IApplicationReadDbConnection _readDb;
    private readonly ILogger<SomeService> _logger;

    public SomeService(
        IApplicationReadDbConnection readDb,
        ILogger<SomeService> logger)
    {
        _readDb = readDb;
        _logger = logger;
    }
}
```

## Verification

- [ ] Service compiles without errors
- [ ] Elimination logic correctly identifies users who can't win even with perfect scores
- [ ] Scenario enumeration produces correct number of combinations
- [ ] Points calculation handles boosts correctly (doubled)
- [ ] Win/tie probabilities sum correctly (can exceed 100% due to overlaps in ties)
- [ ] Service handles edge cases (no remaining matches, single contender, all users eliminated)

## Edge Cases to Consider

- All matches already complete (no scenarios to calculate)
- Only one contender remains (100% win probability)
- All users have identical predictions (100% tie probability for all)
- User has boost applied (all remaining match points doubled)
- Very large number of scenarios (may need performance optimization)
- User didn't submit predictions (treat as 0 points for all remaining matches)

## Notes

- The service uses `IApplicationReadDbConnection` for all data access (CQRS query pattern)
- Internal models are kept separate from DTOs to allow for efficient calculation
- Generic result outcomes represent "any other result of this type" - no one gets exact score points
- The elimination check is O(n) where n is number of users
- Scenario enumeration is O(m^k) where m is average outcomes per match and k is remaining matches

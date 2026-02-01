# Task 4: Constraint Analyser

**Parent Feature:** [Monthly Leaderboard Scenarios](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Implement logic to analyse a user's winning scenarios and generate human-readable constraints describing what results they need for each remaining match.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Leagues/Services/ConstraintAnalyser.cs` | Create | Analyse scenarios to generate constraints |
| `PredictionLeague.Application/Features/Leagues/Services/ScenarioCalculator.cs` | Modify | Integrate constraint analysis |

## Implementation Steps

### Step 1: Create ConstraintAnalyser Class

```csharp
// PredictionLeague.Application/Features/Leagues/Services/ConstraintAnalyser.cs
using PredictionLeague.Contracts.Leagues.Insights;

namespace PredictionLeague.Application.Features.Leagues.Services;

/// <summary>
/// Analyses winning scenarios to generate human-readable constraints
/// describing what results a user needs for each match.
/// </summary>
public class ConstraintAnalyser
{
    /// <summary>
    /// Analyses all scenarios to generate per-match constraints for a contender.
    /// </summary>
    /// <param name="userId">The contender to analyse</param>
    /// <param name="allScenarios">All enumerated scenarios</param>
    /// <param name="remainingMatches">Remaining matches with metadata</param>
    /// <param name="allMembers">All league members for looking up who predicted what</param>
    /// <param name="forRound">True to analyse round-winning scenarios, false for monthly</param>
    /// <returns>List of constraints, one per remaining match</returns>
    public List<MatchConstraint> AnalyseConstraints(
        string userId,
        List<ScenarioResult> allScenarios,
        List<RemainingMatch> remainingMatches,
        List<MemberCalculation> allMembers,
        bool forRound = true)
    {
        // Separate winning vs losing scenarios for this user
        var winningScenarios = allScenarios
            .Where(s => forRound
                ? s.RoundWinnerIds.Contains(userId)
                : s.MonthlyWinnerIds.Contains(userId))
            .ToList();

        var losingScenarios = allScenarios
            .Except(winningScenarios)
            .ToList();

        // If no winning scenarios, user is eliminated (shouldn't happen at this point)
        if (winningScenarios.Count == 0)
        {
            return remainingMatches.Select(m => new MatchConstraint(
                MatchId: m.MatchId,
                HomeTeam: m.HomeTeam,
                AwayTeam: m.AwayTeam,
                RequiredResultTypes: null,
                AnyResultWorks: false,
                ExcludedScorelines: new List<Scoreline>(),
                RequiredScorelines: null
            )).ToList();
        }

        // Analyse each match
        return remainingMatches
            .Select(m => AnalyseMatchConstraint(m, winningScenarios, losingScenarios, allMembers))
            .ToList();
    }

    private MatchConstraint AnalyseMatchConstraint(
        RemainingMatch match,
        List<ScenarioResult> winningScenarios,
        List<ScenarioResult> losingScenarios,
        List<MemberCalculation> allMembers)
    {
        // Get outcomes in winning vs losing scenarios for this match
        var winningOutcomes = winningScenarios
            .Select(s => s.Outcomes.First(o => o.MatchId == match.MatchId))
            .ToList();

        var losingOutcomes = losingScenarios
            .Select(s => s.Outcomes.First(o => o.MatchId == match.MatchId))
            .ToList();

        // Analyse which result types appear in winning scenarios
        var winningResultTypes = winningOutcomes
            .Select(GetResultType)
            .Distinct()
            .ToList();

        var losingResultTypes = losingOutcomes
            .Select(GetResultType)
            .Distinct()
            .ToList();

        // Check if any result works (all three result types in winning scenarios)
        var anyResultWorks = winningResultTypes.Count == 3 ||
            (winningResultTypes.Contains(ResultType.HomeWin) &&
             winningResultTypes.Contains(ResultType.Draw) &&
             winningResultTypes.Contains(ResultType.AwayWin));

        // Find excluded scorelines (exact scores in losing but not winning)
        var excludedScorelines = FindExcludedScorelines(
            match, winningOutcomes, losingOutcomes, allMembers);

        // Find required scorelines (if only specific scores work)
        var requiredScorelines = FindRequiredScorelines(
            match, winningOutcomes, losingOutcomes);

        return new MatchConstraint(
            MatchId: match.MatchId,
            HomeTeam: match.HomeTeam,
            AwayTeam: match.AwayTeam,
            RequiredResultTypes: anyResultWorks ? null : winningResultTypes,
            AnyResultWorks: anyResultWorks,
            ExcludedScorelines: excludedScorelines,
            RequiredScorelines: requiredScorelines
        );
    }

    private List<Scoreline> FindExcludedScorelines(
        RemainingMatch match,
        List<MatchOutcome> winningOutcomes,
        List<MatchOutcome> losingOutcomes,
        List<MemberCalculation> allMembers)
    {
        // Find exact scores that appear ONLY in losing scenarios
        var winningExactScores = winningOutcomes
            .OfType<ExactScoreOutcome>()
            .Select(o => (o.HomeScore, o.AwayScore))
            .Distinct()
            .ToHashSet();

        var losingExactScores = losingOutcomes
            .OfType<ExactScoreOutcome>()
            .Select(o => (o.HomeScore, o.AwayScore))
            .Distinct()
            .ToList();

        var excludedScores = losingExactScores
            .Where(s => !winningExactScores.Contains(s))
            .ToList();

        // For each excluded score, find who predicted it
        return excludedScores.Select(score =>
        {
            var predictors = allMembers
                .Where(m => m.Predictions.TryGetValue(match.MatchId, out var p) &&
                           p.HomeScore == score.HomeScore &&
                           p.AwayScore == score.AwayScore)
                .Select(m => m.UserName)
                .ToList();

            var reason = predictors.Count switch
            {
                0 => null,
                1 => $"{predictors[0]}'s prediction",
                2 => $"{predictors[0]} and {predictors[1]}'s prediction",
                _ => $"{predictors.Count} players predicted this"
            };

            return new Scoreline(score.HomeScore, score.AwayScore, reason);
        }).ToList();
    }

    private List<Scoreline>? FindRequiredScorelines(
        RemainingMatch match,
        List<MatchOutcome> winningOutcomes,
        List<MatchOutcome> losingOutcomes)
    {
        // Check if ONLY specific exact scores work (no generic outcomes in winning scenarios)
        var winningExactScores = winningOutcomes
            .OfType<ExactScoreOutcome>()
            .ToList();

        var winningGenericOutcomes = winningOutcomes
            .OfType<GenericResultOutcome>()
            .ToList();

        // If there are generic outcomes in winning scenarios, specific scores aren't required
        if (winningGenericOutcomes.Count > 0)
            return null;

        // If only exact scores work, return them
        if (winningExactScores.Count > 0 && winningExactScores.Count <= 3)
        {
            return winningExactScores
                .Select(o => new Scoreline(o.HomeScore, o.AwayScore, null))
                .Distinct()
                .ToList();
        }

        return null;
    }

    private ResultType GetResultType(MatchOutcome outcome)
    {
        return outcome switch
        {
            ExactScoreOutcome exact => exact.HomeScore > exact.AwayScore
                ? ResultType.HomeWin
                : exact.HomeScore < exact.AwayScore
                    ? ResultType.AwayWin
                    : ResultType.Draw,

            GenericResultOutcome generic => generic.ResultType,

            _ => ResultType.Draw
        };
    }
}
```

### Step 2: Create Detailed Constraint Description Generator

Add methods to generate more sophisticated descriptions:

```csharp
// Add to ConstraintAnalyser class

/// <summary>
/// Generates a detailed, human-readable description of what a user needs.
/// </summary>
public string GenerateDetailedDescription(
    MatchConstraint constraint,
    string homeTeam,
    string awayTeam)
{
    if (constraint.AnyResultWorks && constraint.ExcludedScorelines.Count == 0)
    {
        return $"Any result works for {homeTeam} vs {awayTeam}";
    }

    var parts = new List<string>();

    // Describe required result type(s)
    if (constraint.RequiredScorelines is { Count: > 0 })
    {
        var scores = string.Join(" or ", constraint.RequiredScorelines.Select(s => $"{homeTeam} {s}"));
        parts.Add($"Need exactly: {scores}");
    }
    else if (constraint.RequiredResultTypes is { Count: > 0 })
    {
        var resultDesc = DescribeResultTypes(constraint.RequiredResultTypes, homeTeam, awayTeam);
        parts.Add($"Need: {resultDesc}");
    }
    else if (constraint.AnyResultWorks)
    {
        parts.Add("Any result type works");
    }

    // Describe exclusions
    if (constraint.ExcludedScorelines.Count > 0)
    {
        var exclusions = constraint.ExcludedScorelines
            .Select(s => s.Reason != null ? $"{s} ({s.Reason})" : s.ToString())
            .ToList();

        if (exclusions.Count <= 3)
        {
            parts.Add($"But NOT: {string.Join(", ", exclusions)}");
        }
        else
        {
            parts.Add($"But NOT {exclusions.Count} specific scorelines");
        }
    }

    return string.Join(". ", parts);
}

private string DescribeResultTypes(IReadOnlyList<ResultType> types, string home, string away)
{
    if (types.Count == 1)
    {
        return types[0] switch
        {
            ResultType.HomeWin => $"{home} win",
            ResultType.Draw => "Draw",
            ResultType.AwayWin => $"{away} win",
            _ => "Unknown"
        };
    }

    if (types.Count == 2)
    {
        // Figure out what's missing
        var missing = new[] { ResultType.HomeWin, ResultType.Draw, ResultType.AwayWin }
            .Except(types)
            .First();

        return missing switch
        {
            ResultType.HomeWin => $"Draw or {away} win",
            ResultType.Draw => $"{home} win or {away} win (no draw)",
            ResultType.AwayWin => $"{home} win or draw",
            _ => "Unknown"
        };
    }

    return "Any result";
}

/// <summary>
/// Summarises constraints across all matches into key insights.
/// </summary>
public List<string> GenerateKeyInsights(
    List<MatchConstraint> constraints,
    List<RemainingMatch> matches)
{
    var insights = new List<string>();

    // Count matches where any result works
    var anyResultCount = constraints.Count(c => c.AnyResultWorks && c.ExcludedScorelines.Count == 0);
    if (anyResultCount > 0)
    {
        insights.Add($"{anyResultCount} match(es) don't affect your chances");
    }

    // Identify critical matches (specific result type required)
    var criticalMatches = constraints
        .Where(c => !c.AnyResultWorks && c.RequiredResultTypes?.Count == 1)
        .ToList();

    foreach (var constraint in criticalMatches)
    {
        var match = matches.First(m => m.MatchId == constraint.MatchId);
        var resultDesc = constraint.RequiredResultTypes![0] switch
        {
            ResultType.HomeWin => $"{match.HomeTeam} must win",
            ResultType.Draw => $"{match.HomeTeam} vs {match.AwayTeam} must draw",
            ResultType.AwayWin => $"{match.AwayTeam} must win",
            _ => "Unknown"
        };
        insights.Add(resultDesc);
    }

    // Count total excluded scorelines
    var totalExclusions = constraints.Sum(c => c.ExcludedScorelines.Count);
    if (totalExclusions > 0)
    {
        insights.Add($"{totalExclusions} specific scoreline(s) would eliminate you");
    }

    return insights;
}
```

### Step 3: Integrate into ScenarioCalculator

Update `AggregateResults` to include constraint analysis:

```csharp
// In ScenarioCalculator.cs, update AggregateResults method:

private List<ContenderInsights> AggregateResults(
    List<MemberCalculation> contenders,
    List<ScenarioResult> scenarios,
    List<RemainingMatch> remainingMatches,
    List<MemberCalculation> allMembers,
    bool isLastRoundOfMonth)
{
    var constraintAnalyser = new ConstraintAnalyser();
    var totalScenarios = scenarios.Count;

    return contenders.Select(c =>
    {
        // ... existing probability calculations ...

        // Analyse constraints for this contender
        var roundConstraints = constraintAnalyser.AnalyseConstraints(
            c.UserId,
            scenarios,
            remainingMatches,
            allMembers,
            forRound: true);

        List<MatchConstraint>? monthlyConstraints = null;
        if (isLastRoundOfMonth)
        {
            monthlyConstraints = constraintAnalyser.AnalyseConstraints(
                c.UserId,
                scenarios,
                remainingMatches,
                allMembers,
                forRound: false);
        }

        // Use round constraints as primary (or merge with monthly)
        var constraints = MergeConstraints(roundConstraints, monthlyConstraints);

        return new ContenderInsights(
            // ... other properties ...
            MatchConstraints: constraints,
            WinningScenarios: null
        );
    })
    .OrderByDescending(c => c.RoundWinProbability)
    .ThenByDescending(c => c.RoundTieProbability)
    .ToList();
}

/// <summary>
/// Merges round and monthly constraints, taking the more restrictive option.
/// </summary>
private List<MatchConstraint> MergeConstraints(
    List<MatchConstraint> roundConstraints,
    List<MatchConstraint>? monthlyConstraints)
{
    if (monthlyConstraints == null)
        return roundConstraints;

    // For now, return round constraints
    // Future enhancement: show both or merge intelligently
    return roundConstraints;
}
```

## Code Patterns to Follow

Follow existing analyser/helper patterns:

```csharp
// Stateless analysis logic
public class SomeAnalyser
{
    public Result Analyse(Input input)
    {
        // Pure function, no side effects
    }
}
```

## Verification

- [ ] Constraint analysis produces valid constraints for all contenders
- [ ] "Any result works" is correctly identified when all result types appear in winning scenarios
- [ ] Excluded scorelines correctly identify scores that only appear in losing scenarios
- [ ] Reasons correctly attribute excluded scores to the players who predicted them
- [ ] Required scorelines only populated when generic outcomes don't work
- [ ] Description generator produces readable, accurate text

## Edge Cases to Consider

- All scenarios are winning (100% probability) - any result works for all matches
- Only one scenario is winning - very specific constraints
- User needs a result that no one predicted (unusual but possible)
- Same scoreline predicted by multiple users
- Match where user needs an exact score (only their prediction works)

## Notes

- Constraints should help users understand what they need, not overwhelm with detail
- "Any result works" should be clearly communicated - user can relax about that match
- Exclusions include the reason (who predicted that score) to make it understandable
- For the "both round and month" view, may need to show separate constraints or merged view

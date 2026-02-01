# Task 3: Live Match Filtering

**Parent Feature:** [Monthly Leaderboard Scenarios](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Implement logic to handle in-progress matches correctly, filtering out impossible exact scores and adjusting the outcome space based on current live scores.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Leagues/Services/ScenarioCalculator.cs` | Modify | Add live match handling |
| `PredictionLeague.Application/Features/Leagues/Services/LiveMatchFilter.cs` | Create | Dedicated helper for live match logic |

## Implementation Steps

### Step 1: Create LiveMatchFilter Helper

```csharp
// PredictionLeague.Application/Features/Leagues/Services/LiveMatchFilter.cs
using PredictionLeague.Contracts.Leagues.Insights;

namespace PredictionLeague.Application.Features.Leagues.Services;

/// <summary>
/// Handles filtering and validation for live (in-progress) matches.
/// </summary>
public static class LiveMatchFilter
{
    /// <summary>
    /// Determines if a predicted scoreline is still achievable given the current live score.
    /// </summary>
    /// <param name="predictedHome">Predicted home score</param>
    /// <param name="predictedAway">Predicted away score</param>
    /// <param name="currentHome">Current live home score</param>
    /// <param name="currentAway">Current live away score</param>
    /// <returns>True if the exact score is still achievable</returns>
    public static bool IsExactScoreAchievable(
        int predictedHome,
        int predictedAway,
        int currentHome,
        int currentAway)
    {
        // A score is achievable if both teams can still reach the predicted score
        // (scores can only go up, never down)
        return predictedHome >= currentHome && predictedAway >= currentAway;
    }

    /// <summary>
    /// Determines if a result type is still achievable given the current live score.
    /// </summary>
    /// <remarks>
    /// All result types are technically achievable at any point (enough goals could be scored),
    /// but this method could be extended to filter out extremely unlikely outcomes.
    /// </remarks>
    public static bool IsResultTypeAchievable(
        ResultType resultType,
        int currentHome,
        int currentAway)
    {
        // All result types remain achievable - goals can be scored by either team
        // In future, could add logic to filter out extremely unlikely results
        // e.g., if it's 90th minute and score is 5-0, away win is very unlikely
        return true;
    }

    /// <summary>
    /// Filters a list of predictions to only those still achievable.
    /// </summary>
    public static List<Prediction> FilterAchievablePredictions(
        IEnumerable<Prediction> predictions,
        int currentHome,
        int currentAway)
    {
        return predictions
            .Where(p => IsExactScoreAchievable(p.HomeScore, p.AwayScore, currentHome, currentAway))
            .ToList();
    }

    /// <summary>
    /// Calculates the maximum points a user can get for a live match.
    /// </summary>
    /// <param name="prediction">User's prediction</param>
    /// <param name="currentHome">Current live home score</param>
    /// <param name="currentAway">Current live away score</param>
    /// <param name="pointsForExact">Points awarded for exact score</param>
    /// <param name="pointsForCorrectResult">Points awarded for correct result</param>
    /// <returns>Maximum possible points for this match</returns>
    public static int GetMaxPossiblePoints(
        Prediction prediction,
        int currentHome,
        int currentAway,
        int pointsForExact,
        int pointsForCorrectResult)
    {
        // If exact score is still achievable, max is exact points
        if (IsExactScoreAchievable(prediction.HomeScore, prediction.AwayScore, currentHome, currentAway))
        {
            return pointsForExact;
        }

        // Otherwise, check if correct result is still possible
        // This depends on whether the predicted result type can still happen

        var predictedResultType = GetResultType(prediction.HomeScore, prediction.AwayScore);

        // What result types are still "natural" outcomes?
        // - If currently home winning (e.g., 2-1): home win is natural, draw/away need goals
        // - All result types technically achievable, but we consider them all possible

        return pointsForCorrectResult;  // Max they can get now is correct result
    }

    /// <summary>
    /// Determines the minimum goals needed to achieve a result type from current score.
    /// </summary>
    public static (int homeGoals, int awayGoals) GoalsNeededForResult(
        ResultType targetResult,
        int currentHome,
        int currentAway)
    {
        return targetResult switch
        {
            ResultType.HomeWin => currentHome > currentAway
                ? (0, 0)  // Already winning
                : (currentAway - currentHome + 1, 0),  // Need to take the lead

            ResultType.Draw => currentHome == currentAway
                ? (0, 0)  // Already drawing
                : currentHome > currentAway
                    ? (0, currentHome - currentAway)  // Away needs to equalise
                    : (currentAway - currentHome, 0),  // Home needs to equalise

            ResultType.AwayWin => currentAway > currentHome
                ? (0, 0)  // Already winning
                : (0, currentHome - currentAway + 1),  // Need to take the lead

            _ => (0, 0)
        };
    }

    private static ResultType GetResultType(int home, int away)
    {
        if (home > away) return ResultType.HomeWin;
        if (home < away) return ResultType.AwayWin;
        return ResultType.Draw;
    }
}
```

### Step 2: Update BuildOutcomeSpace in ScenarioCalculator

Update the `BuildOutcomeSpace` method to properly handle live matches:

```csharp
private void BuildOutcomeSpace(List<RemainingMatch> remainingMatches, List<MemberCalculation> contenders)
{
    foreach (var match in remainingMatches)
    {
        // Get unique predictions from contenders for this match
        var allPredictions = contenders
            .Where(c => c.Predictions.ContainsKey(match.MatchId))
            .Select(c => c.Predictions[match.MatchId])
            .ToList();

        List<Prediction> achievablePredictions;

        if (match.IsLive)
        {
            // Filter to only achievable predictions for live matches
            achievablePredictions = LiveMatchFilter.FilterAchievablePredictions(
                allPredictions,
                match.LiveHomeScore!.Value,
                match.LiveAwayScore!.Value);

            _logger.LogDebug(
                "Live Match (ID: {MatchId}) at {Home}-{Away}: {Total} predictions, {Achievable} still achievable",
                match.MatchId,
                match.LiveHomeScore,
                match.LiveAwayScore,
                allPredictions.Count,
                achievablePredictions.Count);
        }
        else
        {
            // All predictions achievable for upcoming matches
            achievablePredictions = allPredictions;
        }

        // Add each unique achievable prediction as a possible outcome
        var uniquePredictions = achievablePredictions.Distinct().ToList();
        foreach (var pred in uniquePredictions)
        {
            match.PossibleOutcomes.Add(
                new ExactScoreOutcome(match.MatchId, pred.HomeScore, pred.AwayScore));
        }

        // Add generic result types (always included, all remain achievable)
        match.PossibleOutcomes.Add(new GenericResultOutcome(match.MatchId, ResultType.HomeWin));
        match.PossibleOutcomes.Add(new GenericResultOutcome(match.MatchId, ResultType.Draw));
        match.PossibleOutcomes.Add(new GenericResultOutcome(match.MatchId, ResultType.AwayWin));

        _logger.LogDebug(
            "Match (ID: {MatchId}) {Home} vs {Away}: {OutcomeCount} possible outcomes ({IsLive})",
            match.MatchId,
            match.HomeTeam,
            match.AwayTeam,
            match.PossibleOutcomes.Count,
            match.IsLive ? "LIVE" : "upcoming");
    }
}
```

### Step 3: Update Points Calculation for Live Matches

Update the `CalculatePointsForOutcome` method to correctly handle live match constraints:

```csharp
private int CalculatePointsForOutcome(
    Prediction prediction,
    MatchOutcome outcome,
    RemainingMatch match,
    int pointsForExact,
    int pointsForCorrectResult)
{
    return outcome switch
    {
        ExactScoreOutcome exact => CalculatePointsForExactOutcome(
            prediction, exact, match, pointsForExact, pointsForCorrectResult),

        GenericResultOutcome generic => CalculatePointsForGenericOutcome(
            prediction, generic, match, pointsForCorrectResult),

        _ => 0
    };
}

private int CalculatePointsForExactOutcome(
    Prediction prediction,
    ExactScoreOutcome outcome,
    RemainingMatch match,
    int pointsForExact,
    int pointsForCorrectResult)
{
    // Check if user's prediction matches this exact outcome
    var isExactMatch = prediction.HomeScore == outcome.HomeScore &&
                       prediction.AwayScore == outcome.AwayScore;

    if (isExactMatch)
    {
        // For live matches, verify the exact score is still achievable
        if (match.IsLive)
        {
            var canAchieveExact = LiveMatchFilter.IsExactScoreAchievable(
                prediction.HomeScore,
                prediction.AwayScore,
                match.LiveHomeScore!.Value,
                match.LiveAwayScore!.Value);

            if (!canAchieveExact)
            {
                // Exact score no longer possible - can only get correct result at best
                return GetResultType(prediction) == GetResultType(outcome.HomeScore, outcome.AwayScore)
                    ? pointsForCorrectResult
                    : 0;
            }
        }

        return pointsForExact;
    }

    // Not exact match - check for correct result
    return GetResultType(prediction) == GetResultType(outcome.HomeScore, outcome.AwayScore)
        ? pointsForCorrectResult
        : 0;
}

private int CalculatePointsForGenericOutcome(
    Prediction prediction,
    GenericResultOutcome outcome,
    RemainingMatch match,
    int pointsForCorrectResult)
{
    // Generic outcomes never award exact score points (by definition)
    // User gets correct result points if their predicted result type matches
    return GetResultType(prediction) == outcome.ResultType
        ? pointsForCorrectResult
        : 0;
}

private ResultType GetResultType(Prediction p) => GetResultType(p.HomeScore, p.AwayScore);

private ResultType GetResultType(int home, int away)
{
    if (home > away) return ResultType.HomeWin;
    if (home < away) return ResultType.AwayWin;
    return ResultType.Draw;
}
```

### Step 4: Update Elimination Check for Live Matches

The elimination check must also account for live match constraints:

```csharp
private Dictionary<string, int> SimulateBestCase(MemberCalculation member, CalculationData data)
{
    var results = new Dictionary<string, int>();

    foreach (var m in data.Members)
    {
        var points = m.CurrentRoundPoints;

        foreach (var match in data.RemainingMatches)
        {
            // The "actual result" is the member-under-test's prediction
            if (!member.Predictions.TryGetValue(match.MatchId, out var hypotheticalResult))
                continue;

            // For live matches, check if this hypothetical result is achievable
            if (match.IsLive)
            {
                var isAchievable = LiveMatchFilter.IsExactScoreAchievable(
                    hypotheticalResult.HomeScore,
                    hypotheticalResult.AwayScore,
                    match.LiveHomeScore!.Value,
                    match.LiveAwayScore!.Value);

                if (!isAchievable)
                {
                    // Member's prediction for this match is no longer achievable
                    // Use a "best achievable" result instead - same result type
                    hypotheticalResult = GetBestAchievableResult(
                        hypotheticalResult,
                        match.LiveHomeScore!.Value,
                        match.LiveAwayScore!.Value);
                }
            }

            // Calculate points for user m given this hypothetical result
            if (m.Predictions.TryGetValue(match.MatchId, out var mPrediction))
            {
                var matchPoints = CalculatePointsForHypothetical(
                    mPrediction,
                    hypotheticalResult,
                    match,
                    data.RoundInfo.PointsForExactScore,
                    data.RoundInfo.PointsForCorrectResult);

                if (m.HasBoost)
                    matchPoints *= 2;

                points += matchPoints;
            }
        }

        results[m.UserId] = points;
    }

    return results;
}

/// <summary>
/// For a live match where the original prediction is no longer achievable,
/// return the best achievable result of the same result type.
/// </summary>
private Prediction GetBestAchievableResult(Prediction original, int currentHome, int currentAway)
{
    var resultType = GetResultType(original);

    // Return the minimum achievable score that matches the result type
    return resultType switch
    {
        ResultType.HomeWin => currentHome > currentAway
            ? new Prediction(currentHome, currentAway)  // Current score works
            : new Prediction(currentAway + 1, currentAway),  // Min home win

        ResultType.Draw => currentHome == currentAway
            ? new Prediction(currentHome, currentAway)  // Current score is a draw
            : new Prediction(
                Math.Max(currentHome, currentAway),
                Math.Max(currentHome, currentAway)),  // Equalise at higher score

        ResultType.AwayWin => currentAway > currentHome
            ? new Prediction(currentHome, currentAway)  // Current score works
            : new Prediction(currentHome, currentHome + 1),  // Min away win

        _ => new Prediction(currentHome, currentAway)
    };
}
```

## Code Patterns to Follow

Follow existing helper class patterns - static methods for stateless logic:

```csharp
// Example pattern from codebase
public static class SomeHelper
{
    public static bool SomeCheck(int value) => value > 0;
}
```

## Verification

- [ ] `IsExactScoreAchievable` correctly identifies impossible scores
- [ ] Live match at 2-1 filters out predictions like 2-0, 1-0, 1-1, 0-0
- [ ] Live match at 2-1 allows predictions like 2-1, 3-1, 2-2, 3-2
- [ ] Generic result types are always included for live matches
- [ ] Elimination check accounts for impossible exact scores in best-case calculation
- [ ] Points calculation gives correct result (not exact) for impossible predictions

## Edge Cases to Consider

- Live match at 0-0 (all predictions still achievable)
- Live match at 5-0 (many predictions now impossible)
- User predicted the exact current score (still achievable, could stay the same)
- User predicted a scoreline that becomes impossible mid-match
- Match goes from Scheduled to InProgress during calculation (handle gracefully)

## Notes

- All result types (home/draw/away) remain technically possible regardless of current score
- The filter only affects EXACT score possibilities, not result type possibilities
- For elimination check, if a user's "best case" prediction is impossible, we substitute the best achievable result of the same type
- This ensures a user who predicted 2-0 when it's 2-1 still gets credit for "home win" scenarios

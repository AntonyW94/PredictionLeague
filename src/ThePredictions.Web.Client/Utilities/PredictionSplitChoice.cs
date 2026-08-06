using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Web.Client.Utilities;

public static class PredictionSplitChoice
{
    /// <summary>
    /// Returns the given user's pick for a match - "H" (home win), "D" (draw) or "A" (away win) -
    /// or null if the user has no usable prediction for it. Used to bold the user's own choice in
    /// the prediction split. Computed by the component that owns the results data (and the resolved
    /// current-user id) rather than inside the split itself.
    /// </summary>
    public static string? For(IEnumerable<PredictionResultDto>? results, string? userId, int matchId)
    {
        var prediction = FindPrediction(results, userId, matchId);
        if (prediction is null)
            return null;

        return Outcome(prediction.HomeScore!.Value, prediction.AwayScore!.Value);
    }

    /// <summary>
    /// The user's prediction for this match, or null when there is no signed-in user, no results,
    /// no row for them, or the row carries no usable score.
    /// </summary>
    private static PredictionScoreDto? FindPrediction(IEnumerable<PredictionResultDto>? results, string? userId, int matchId)
    {
        if (string.IsNullOrEmpty(userId) || results is null)
            return null;

        var prediction = results
            .FirstOrDefault(r => r.UserId == userId)?
            .Predictions.FirstOrDefault(p => p.MatchId == matchId);

        return prediction?.HomeScore is null || prediction.AwayScore is null ? null : prediction;
    }

    private static string Outcome(int homeScore, int awayScore)
    {
        if (homeScore > awayScore)
            return "H";

        return homeScore < awayScore ? "A" : "D";
    }
}

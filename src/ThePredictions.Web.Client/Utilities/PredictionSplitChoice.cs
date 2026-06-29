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
        if (string.IsNullOrEmpty(userId) || results is null)
            return null;

        var prediction = results
            .FirstOrDefault(r => r.UserId == userId)?
            .Predictions.FirstOrDefault(p => p.MatchId == matchId);

        if (prediction?.HomeScore is null || prediction.AwayScore is null)
            return null;

        if (prediction.HomeScore > prediction.AwayScore)
            return "H";

        if (prediction.HomeScore < prediction.AwayScore)
            return "A";

        return "D";
    }
}

namespace ThePredictions.Domain.Services;

/// <summary>
/// What a round is worth to a player in a league, before any boost is applied.
/// </summary>
/// <remarks>
/// The scoring rule of the whole game, and it lived only in SQL - inside the <c>MERGE</c> that rebuilt every league's
/// round results:
///
/// <code>
/// (rr.[ExactScoreCount] * l.[PointsForExactScore]) + (rr.[CorrectResultCount] * l.[PointsForCorrectResult])
/// </code>
///
/// There was no C# copy to disagree with it, which is exactly why nothing tested it. The points per exact score and per
/// correct result are set per league, so two leagues watching the same fixtures award different totals for the same
/// predictions - which is the product feature this arithmetic implements.
///
/// A miss is worth nothing, which is why <see cref="OutcomeCounts.IncorrectCount"/> is not in the sum.
/// </remarks>
public static class LeagueScoring
{
    public static int BasePoints(OutcomeCounts counts, int pointsForExactScore, int pointsForCorrectResult) =>
        (counts.ExactScoreCount * pointsForExactScore) + (counts.CorrectResultCount * pointsForCorrectResult);
}

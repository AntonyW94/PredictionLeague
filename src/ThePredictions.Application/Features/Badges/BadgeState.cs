using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services.Badges;

namespace ThePredictions.Application.Features.Badges;

/// <summary>
/// Works out what a player has earned and how far along they are with the rest, from rows.
///
/// Progress is never stored - it is recomputed on every read - so every metric here is a rule about what counts,
/// and each one used to be a statement of its own.
/// </summary>
internal static class BadgeState
{
    public static BadgeUserState From(BadgeStateData data) =>
        new(EarnedFrom(data.Awards), MetricsFrom(data));

    /// <summary>
    /// The badges held, keyed so the catalogue can look one up, with how many times each was won and when it was
    /// last won.
    /// </summary>
    /// <remarks>
    /// The count is every award, not the distinct badges: a repeatable badge like round winner is worth showing as
    /// "won 3 times". That is the opposite of how the leaderboard counts, which is why the rows arrive ungrouped.
    /// </remarks>
    private static IReadOnlyDictionary<string, EarnedBadge> EarnedFrom(IReadOnlyList<BadgeAwardRow> awards) =>
        awards
            .GroupBy(award => award.BadgeKey)
            .ToDictionary(
                group => group.Key,
                group => new EarnedBadge(group.Key, group.Count(), group.Max(award => award.AwardedUtc)));

    private static BadgeProgressMetrics MetricsFrom(BadgeStateData data)
    {
        // Every round anybody has a result for. A round the player sat out belongs in here, because it breaks a
        // streak rather than being passed over.
        var scoredRounds = data.Rounds.Where(round => round.HasAnyResult).ToList();

        var theirRounds = scoredRounds
            .Where(round => round.UserExactScoreCount.HasValue)
            .Select(round => new ScoredRound(round.SeasonId, round.UserExactScoreCount!.Value))
            .ToList();

        if (theirRounds.Count == 0)
            return new BadgeProgressMetrics(0, 0, 0, 0, data.LeaguesJoined, EverPresentFrom(data));

        // Their latest season, which is the one two of these metrics are about - not the site's current season. A
        // player who has not played since last year still sees what they did then, rather than a row of zeroes.
        var latestSeasonId = theirRounds.Max(round => round.SeasonId);

        return new BadgeProgressMetrics(
            SeasonExactTotal: theirRounds.Where(round => round.SeasonId == latestSeasonId).Sum(round => round.ExactScoreCount),
            BestExactsInRound: theirRounds.Max(round => round.ExactScoreCount),
            BestStreak: scoredRounds
                .GroupBy(round => round.SeasonId)
                .Max(season => Streak.Longest(CountedInRoundOrder(season))),
            CurrentStreak: Streak.Current(CountedInRoundOrder(scoredRounds.Where(round => round.SeasonId == latestSeasonId))),
            LeaguesJoined: data.LeaguesJoined,
            EverPresent: EverPresentFrom(data));
    }

    /// <summary>
    /// Whether each round counted towards a streak, in round order: one exact score is enough, and a round with no
    /// result for this player counts as a miss.
    /// </summary>
    /// <remarks>
    /// A streak lives inside a season - it does not carry across the summer - so the caller groups first. The best
    /// run is the best of any season; the current run is only ever the latest season's, and only if it reaches that
    /// season's last scored round.
    /// </remarks>
    private static IEnumerable<bool> CountedInRoundOrder(IEnumerable<BadgeRoundRow> rounds) =>
        rounds
            .OrderBy(round => round.RoundNumber)
            .Select(round => round.UserExactScoreCount is >= 1);

    /// <summary>
    /// How far through their latest season the player has predicted every single match, or nothing at all if there
    /// is no season to be ever-present through yet.
    /// </summary>
    /// <remarks>
    /// "Their latest season" is the last one they predicted in, which is not the same as the last one they scored
    /// in: a player who has predicted a round that has not been scored yet is judged on that season.
    ///
    /// Only finished rounds count, so the badge cannot be lost to a round still in play. A round with no matches
    /// counts against them - it cannot have been fully predicted - which sounds harsh but is the state a round sits
    /// in before its fixtures are loaded, and it is what the previous statement did.
    /// </remarks>
    private static EverPresentProgress? EverPresentFrom(BadgeStateData data)
    {
        var predictedIn = data.Rounds.Where(round => round.UserPredictionCount > 0).ToList();

        if (predictedIn.Count == 0)
            return null;

        var latestSeasonId = predictedIn.Max(round => round.SeasonId);

        var completedRounds = data.Rounds
            .Where(round => round.SeasonId == latestSeasonId && round.Status == RoundStatus.Completed)
            .ToList();

        if (completedRounds.Count == 0)
            return null;

        var fullyPredicted = completedRounds
            .Count(round => round.MatchCount > 0 && round.UserPredictionCount >= round.MatchCount);

        return new EverPresentProgress(fullyPredicted, completedRounds.Count, fullyPredicted < completedRounds.Count);
    }

    /// <summary>A round this player has a result for, and how many exact scores they got in it.</summary>
    private sealed record ScoredRound(int SeasonId, int ExactScoreCount);
}

using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Domain.Services;
using ThePredictions.Domain.Services.Badges;

namespace ThePredictions.Application.Features.Badges;

/// <summary>
/// The site-wide badges table: who is on it, what each player has collected, and the positions they hold.
///
/// One place, used by both the table and the dashboard tile's "3rd of 44" line, so the two cannot disagree about
/// the same player - which they did, because each worked its own out.
/// </summary>
internal static class BadgeStandings
{
    public static IReadOnlyList<Ranked<BadgeStanding>> Of(BadgeLeaderboardData data)
    {
        var awardsByPlayer = data.Awards
            .GroupBy(award => award.UserId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var standings = data.Players
            .Where(IsPlayer)
            .Select(player => StandingFor(player, awardsByPlayer.GetValueOrDefault(player.UserId) ?? []))
            .ToList();

        return Ranking.ByDescending(standings, standing => standing.Tally, standing => standing.FullName);
    }

    /// <summary>
    /// Whether an account belongs on a leaderboard at all: it needs a name.
    /// </summary>
    /// <remarks>
    /// Registration creates the account before the player fills in who they are, so a nameless row is a sign-up that
    /// was never finished. Listing them would put blanks on a public table, and counting them would inflate the "of
    /// 44 players" every player is measured against.
    /// </remarks>
    private static bool IsPlayer(BadgePlayerRow player) => !string.IsNullOrWhiteSpace(player.FirstName);

    /// <summary>
    /// One player's tally: how many <b>different</b> badges they hold, and when they last earned one.
    /// </summary>
    /// <remarks>
    /// Distinct badges, so winning round winner five times counts once here - this table is about how much of the
    /// collection someone has, not how often they have won. The badges page counts the same awards the other way.
    ///
    /// A player with no badges has no date, and that is left as nothing rather than filled in with a stand-in date,
    /// because the tally is what decides positions and "never" has to compare as never.
    /// </remarks>
    private static BadgeStanding StandingFor(BadgePlayerRow player, List<BadgePlayerAwardRow> awards) =>
        new(player.UserId,
            PlayerDisplayName.Format(player.FirstName, player.LastName),
            PlayerDisplayName.FormatFull(player.FirstName, player.LastName),
            new BadgeTally(
                awards.Select(award => award.BadgeKey).Distinct().Count(),
                awards.Count == 0 ? null : awards.Max(award => award.AwardedUtc)));
}

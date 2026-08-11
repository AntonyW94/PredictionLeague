using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>
/// What the season-pass pages decide about a season: whether this player already has it, whether they could still take it
/// up, and whether they can have a trial.
///
/// Four screens asked these questions with four statements. Written once, they turn out to be a handful of lines, and the
/// available-passes and past-passes pages turn out to be complements of the same condition.
/// </summary>
internal static class SeasonPassAvailability
{
    /// <summary>Whether this player already holds a pass for the season.</summary>
    public static bool AlreadyHeld(SeasonPassPagesData data, int seasonId) =>
        data.HeldPasses.Any(pass => pass.SeasonId == seasonId);

    /// <summary>
    /// Whether a first pass would be a free trial: only for a player who has never held one, of any season.
    /// </summary>
    /// <remarks>
    /// A trial is the one-off way in, so the test is about the player rather than the season - which is why a player who
    /// took a trial two seasons ago cannot have another. Two statements asked this as
    /// <c>(SELECT COUNT(*) FROM [SeasonPasses] WHERE [UserId] = @UserId) = 0</c>.
    /// </remarks>
    public static bool IsTrialEligible(SeasonPassPagesData data) => data.HeldPasses.Count == 0;

    /// <summary>
    /// Whether there is still a league in the season somebody could join.
    /// </summary>
    /// <remarks>
    /// This is what makes a pass worth offering: a season nobody can enter any more is a season a pass buys nothing in. It
    /// reads through <see cref="LeagueEntry.IsOpen"/>, which is where "no deadline means closed" is stated - the old
    /// statements got that right only because SQL drops a null from a comparison, rather than because anybody decided it.
    /// </remarks>
    public static bool IsEntryOpen(SeasonPassPagesData data, int seasonId, DateTime utcNow) =>
        data.Leagues.Any(league => league.SeasonId == seasonId && LeagueEntry.IsOpen(league.EntryDeadlineUtc, utcNow));

    /// <summary>The soonest entry deadline still to come in the season, or nothing if none is.</summary>
    public static DateTime? NextEntryDeadline(SeasonPassPagesData data, int seasonId, DateTime utcNow) =>
        data.Leagues
            .Where(league => league.SeasonId == seasonId && LeagueEntry.IsOpen(league.EntryDeadlineUtc, utcNow))
            .Select(league => league.EntryDeadlineUtc)
            .OrderBy(deadline => deadline)
            .FirstOrDefault();

    /// <summary>Whether the season has any leagues at all, which is how "the season actually ran" is judged.</summary>
    public static bool HasAnyLeague(SeasonPassPagesData data, int seasonId) =>
        data.Leagues.Any(league => league.SeasonId == seasonId);

    /// <summary>How many players are taking part in the season.</summary>
    public static int PlayerCount(SeasonPassPagesData data, int seasonId) =>
        data.HolderCounts.SingleOrDefault(count => count.SeasonId == seasonId)?.HolderCount ?? 0;

    /// <summary>Whether the season has to be paid for, or is a free one.</summary>
    public static bool RequiresPayment(SeasonPassSeasonRow season) => season.StandardPrice is not null;

    /// <summary>Whether a pass carries text-message reminders, which is what the premium tier buys.</summary>
    public static bool HasSmsReminders(HeldSeasonPassRow pass) => pass.Tier == nameof(SeasonPassTier.Premium);

    /// <summary>Newest season first, on every one of these pages.</summary>
    public static IEnumerable<SeasonPassSeasonRow> NewestFirst(IEnumerable<SeasonPassSeasonRow> seasons) =>
        seasons.OrderByDescending(season => season.StartDateUtc);
}

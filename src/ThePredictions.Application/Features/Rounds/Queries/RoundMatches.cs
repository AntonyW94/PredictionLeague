using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// What both round views do with the fixtures they read: put them in order, and shape one for a screen.
/// </summary>
/// <remarks>
/// Shared rather than restated, because the two statements this comes from had nineteen positional columns each and
/// disagreed about the order they came back in. The mapping is the sort of thing that goes wrong silently when it is
/// written twice.
/// </remarks>
internal static class RoundMatches
{
    /// <summary>
    /// Kick-off order, and then by home team so a simultaneous pair reads the same way every time.
    /// </summary>
    /// <remarks>
    /// The players' view ordered by kick-off alone and the administrator's editor did not order at all, which left the
    /// fixtures in whatever order a join produced - so adding or removing one could reshuffle the page. Two fixtures
    /// kicking off together are common on a Saturday afternoon, which is exactly when the tie-break earns its keep.
    /// </remarks>
    public static IEnumerable<RoundMatchRow> InKickOffOrder(IEnumerable<RoundMatchRow> matches) =>
        matches
            .OrderBy(match => match.MatchDateTimeUtc)
            .ThenBy(match => match.HomeTeamName, StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// Whether both teams in a fixture are known yet - the row-level twin of <see cref="Match.AreTeamsConfirmed"/>.
    /// </summary>
    /// <remarks>
    /// A knockout tie is scheduled before its teams are decided, and until then it carries two placeholder names instead. The
    /// prediction page uses this to show a fixture that cannot be predicted yet.
    /// </remarks>
    public static bool AreTeamsConfirmed(RoundMatchRow match) =>
        match.HomeTeamId.HasValue && match.AwayTeamId.HasValue;

    /// <summary>Whether a fixture has been called off, in the one place that decides it.</summary>
    /// <remarks>
    /// The row-level twin of <see cref="Match.IsPostponed"/>, for the read paths that hold rows rather than entities.
    /// The statement it replaces listed every other status instead of naming this one, which would have silently
    /// dropped any status added later.
    /// </remarks>
    public static bool IsPostponed(RoundMatchRow match) => Match.IsPostponedStatus(match.Status);

    public static MatchInRoundDto ToDto(RoundMatchRow match) =>
        new(match.Id,
            match.MatchDateTimeUtc,
            match.MatchNumber,
            match.HomeTeamId,
            match.HomeTeamName,
            match.HomeTeamShortName,
            match.HomeTeamAbbreviation,
            match.HomeTeamLogoUrl,
            match.AwayTeamId,
            match.AwayTeamName,
            match.AwayTeamShortName,
            match.AwayTeamAbbreviation,
            match.AwayTeamLogoUrl,
            match.ActualHomeTeamScore,
            match.ActualAwayTeamScore,
            match.Status,
            match.PlaceholderHomeName,
            match.PlaceholderAwayName,
            match.CustomLockTimeUtc);
}

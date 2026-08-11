using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services;

/// <summary>
/// How far through a group of rounds a league is - used for the month and stage pickers, which each offer a period
/// and say how much of it is left.
/// </summary>
/// <param name="RoundsRemaining">
/// Rounds not yet completed. Draft rounds count: a month offered because it has one published round still reports
/// its unpublished rounds as to come, which is what the old <c>SUM(CASE WHEN Status &lt;&gt; @Completed ...)</c> did.
/// </param>
/// <param name="RoundsCompleted">Rounds finished.</param>
/// <param name="HasVisibleRound">
/// Whether the period is worth offering at all. A period whose rounds are all drafts is not: nothing in it exists as
/// far as players are concerned.
/// </param>
/// <remarks>
/// The two pickers stated all three of these as near-identical <c>SUM(CASE WHEN ...)</c> columns over the same table,
/// differing only in what they grouped by - the calendar month, or the tournament stage. Same rule, two groupings.
/// </remarks>
public sealed record RoundProgress(int RoundsRemaining, int RoundsCompleted, bool HasVisibleRound)
{
    public static RoundProgress Of(IEnumerable<RoundStatus> statuses)
    {
        var all = statuses.ToList();

        return new RoundProgress(
            RoundsRemaining: all.Count(status => status != RoundStatus.Completed),
            RoundsCompleted: all.Count(status => status == RoundStatus.Completed),
            HasVisibleRound: all.Any(status => status != RoundStatus.Draft));
    }
}

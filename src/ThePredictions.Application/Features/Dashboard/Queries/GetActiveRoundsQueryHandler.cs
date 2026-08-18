using MediatR;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The rounds on a player's dashboard: what they can still predict, and what they have just predicted.
/// </summary>
public class GetActiveRoundsQueryHandler(
    IActiveRoundsQuery activeRoundsQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetActiveRoundsQuery, IEnumerable<ActiveRoundDto>>
{
    public async Task<IEnumerable<ActiveRoundDto>> Handle(
        GetActiveRoundsQuery request,
        CancellationToken cancellationToken)
    {
        // Read the clock once. Whether a round is still active and whether each match's prediction split may be shown are
        // decided against the same instant, which two separate reads could not guarantee.
        var utcNow = dateTimeProvider.UtcNow;

        var data = await activeRoundsQuery.ExecuteAsync(request.UserId, cancellationToken);

        var matchesByRound = data.Matches
            .GroupBy(match => match.RoundId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return data.Rounds
            .Select(round => new ActiveRound(round, MatchesOf(matchesByRound, round.RoundId)))
            .Where(round => IsWorthShowing(round, utcNow))
            .OrderBy(round => round.Candidate.Status == RoundStatus.InProgress ? 0 : 1)
            .ThenBy(round => round.Candidate.DeadlineUtc)
            .Select(round => ToDto(round, utcNow))
            .ToList();
    }

    /// <summary>
    /// Whether a round belongs on the dashboard.
    /// </summary>
    /// <remarks>
    /// It must have a match with both teams known - a round of placeholders is nothing a player can act on - and either still
    /// be open, or already hold their predictions. The second half is why a round stays on the tile after its deadline: a
    /// player who has predicted wants to see how it went, and a player who has not has nothing to gain from being shown it.
    /// </remarks>
    private static bool IsWorthShowing(ActiveRound round, DateTime utcNow)
    {
        if (!round.Candidate.HasConfirmedMatch)
            return false;

        if (LatestDeadlineOf(round) > utcNow)
            return true;

        return round.Candidate.HasUserPredicted;
    }

    /// <summary>
    /// The last moment anything in this round can be predicted, over its matches that have not been postponed.
    /// </summary>
    private static DateTime LatestDeadlineOf(ActiveRound round) =>
        PredictionWindow.LatestDeadline(
            round.Candidate.DeadlineUtc,
            round.Matches.Select(match => match.CustomLockTimeUtc));

    private static ActiveRoundDto ToDto(ActiveRound round, DateTime utcNow)
    {
        var candidate = round.Candidate;

        return new ActiveRoundDto(
            candidate.RoundId,
            candidate.SeasonName,
            candidate.RoundNumber,
            candidate.RoundDisplayName,
            candidate.CompetitionType == CompetitionType.Tournament,
            candidate.DeadlineUtc,
            LatestDeadlineOf(round),
            candidate.HasUserPredicted,
            candidate.Status,
            round.Matches.Select(match => ToMatchDto(match, candidate.DeadlineUtc, utcNow)).ToList(),
            OutcomeSummaryOf(round, utcNow));
    }

    /// <summary>
    /// One match, with the prediction split shown only once that match itself has locked.
    /// </summary>
    /// <remarks>
    /// The counts are zeroed rather than merely hidden, so the numbers never travel to a browser that could read them anyway.
    /// In a combined round the earlier matches reveal at the round deadline while the later ones stay hidden until their own
    /// lock time, which is why this asks about the match rather than the round.
    /// </remarks>
    private static ActiveRoundMatchDto ToMatchDto(ActiveRoundMatchRow match, DateTime roundDeadlineUtc, DateTime utcNow)
    {
        var revealSplit = PredictionWindow.HasLocked(match.CustomLockTimeUtc, roundDeadlineUtc, utcNow);

        return new ActiveRoundMatchDto(
            match.HomeTeamLogoUrl,
            match.AwayTeamLogoUrl,
            match.HomeTeamShortName,
            match.AwayTeamShortName,
            match.PredictedHomeScore,
            match.PredictedAwayScore,
            match.Outcome,
            match.Status,
            match.ActualHomeScore,
            match.ActualAwayScore,
            match.MatchDateTimeUtc,
            match.MatchNumber,
            match.AreTeamsConfirmed,
            match.PlaceholderHomeName,
            match.PlaceholderAwayName,
            revealSplit,
            revealSplit ? match.HomeCount : 0,
            revealSplit ? match.DrawCount : 0,
            revealSplit ? match.AwayCount : 0,
            match.CustomLockTimeUtc);
    }

    /// <summary>
    /// How the player's round went, once it has started and if they took part.
    /// </summary>
    /// <remarks>
    /// Nothing before the round deadline, because until then the scoring is provisional and mostly empty. Nothing for a player
    /// who did not predict, because a summary of no predictions is three zeroes pretending to be a result.
    /// </remarks>
    private static OutcomeSummaryDto? OutcomeSummaryOf(ActiveRound round, DateTime utcNow)
    {
        if (round.Candidate.DeadlineUtc > utcNow)
            return null;

        if (!round.Candidate.HasUserPredicted)
            return null;

        // The same counting the stored tally uses, which was a MERGE with three SUM(CASE WHEN ...) columns. One rule,
        // one definition of which predictions count towards it.
        var counts = OutcomeTally.For(round.Matches.Select(match => match.Outcome));

        return new OutcomeSummaryDto(counts.ExactScoreCount, counts.CorrectResultCount, counts.IncorrectCount);
    }

    /// <summary>
    /// A round's matches, in kick-off order and then by home team so a simultaneous pair reads the same way every time.
    /// </summary>
    private static List<ActiveRoundMatchRow> MatchesOf(
        IReadOnlyDictionary<int, List<ActiveRoundMatchRow>> matchesByRound,
        int roundId)
    {
        var matches = matchesByRound.GetValueOrDefault(roundId) ?? [];

        return matches
            .OrderBy(match => match.MatchDateTimeUtc)
            .ThenBy(match => match.HomeTeamShortName, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    /// <summary>One round and its matches, once they have been brought together.</summary>
    private sealed record ActiveRound(ActiveRoundCandidateRow Candidate, List<ActiveRoundMatchRow> Matches);
}

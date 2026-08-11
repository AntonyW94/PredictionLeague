using MediatR;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// One round-results email per player who took part: how they did, how each of their leagues went, and what is next.
/// </summary>
public class GetRoundDigestQueryHandler(IRoundDigestQuery roundDigestQuery)
    : IRequestHandler<GetRoundDigestQuery, IReadOnlyList<UserRoundDigest>>
{
    public async Task<IReadOnlyList<UserRoundDigest>> Handle(GetRoundDigestQuery request, CancellationToken cancellationToken)
    {
        var data = await roundDigestQuery.ExecuteAsync(request.RoundId, cancellationToken);

        var round = data.SeasonRounds.SingleOrDefault(candidate => candidate.Id == request.RoundId);

        if (round is null)
            return [];

        var nextRound = NextAfter(data.SeasonRounds, round);
        var scoresByLeague = data.LeagueScores.GroupBy(score => score.LeagueId).ToDictionary(league => league.Key, league => league.ToList());

        return data.Players
            .Where(HasTakenPart)
            .OrderBy(player => player.UserId, StringComparer.Ordinal)
            .Select(player => Digest(player, round, nextRound, LeaguesFor(player.UserId, data.Memberships, scoresByLeague)))
            .Where(digest => digest.Leagues.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Whether this player gets an email at all.
    /// </summary>
    /// <remarks>
    /// Being scored is not enough - everybody in the round is scored, including the players who forgot. An email telling
    /// somebody they got nothing right in a round they never entered is the one thing this must not send, and a round
    /// they did enter is one they predicted at least one fixture of.
    /// </remarks>
    private static bool HasTakenPart(RoundDigestPlayerRow player) => player.PredictionCount > 0;

    /// <summary>
    /// The round the email points forward to: the earliest one after this in the same season.
    /// </summary>
    /// <remarks>
    /// By round number rather than by deadline, so a round rescheduled into next month is still the one that comes next.
    /// Nothing at all after the final round, and the email leaves that line out rather than inventing a fixture.
    /// </remarks>
    private static RoundDigestRoundRow? NextAfter(IEnumerable<RoundDigestRoundRow> seasonRounds, RoundDigestRoundRow round) =>
        seasonRounds
            .Where(candidate => candidate.RoundNumber > round.RoundNumber)
            .OrderBy(candidate => candidate.RoundNumber)
            .FirstOrDefault();

    /// <summary>
    /// Every league this player is in that had this round scored, in the order the email lists them.
    /// </summary>
    /// <remarks>
    /// A league where the player has no score for the round is left out. That is not tidiness - it is the state a league
    /// is in between a round finishing and its points being worked out, and a row of zeroes would read as a bad round
    /// rather than an unfinished calculation. A player with no such league gets no email, which is the caller's cue.
    /// </remarks>
    private static List<LeagueRoundDigest> LeaguesFor(
        string userId,
        IEnumerable<RoundDigestMembershipRow> memberships,
        IReadOnlyDictionary<int, List<RoundLeagueScoreRow>> scoresByLeague) =>
        memberships
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.LeagueName, StringComparer.InvariantCultureIgnoreCase)
            .Select(membership => Entry(membership, userId, scoresByLeague.GetValueOrDefault(membership.LeagueId) ?? []))
            .OfType<LeagueRoundDigest>()
            .ToList();

    private static LeagueRoundDigest? Entry(RoundDigestMembershipRow membership, string userId, List<RoundLeagueScoreRow> leagueScores)
    {
        var theirScore = leagueScores.SingleOrDefault(score => score.UserId == userId);

        if (theirScore is null)
            return null;

        // Safe because their own score is one of these, so there is always a top scorer once we are here.
        var topScorer = TopScorerOf(leagueScores);

        return new LeagueRoundDigest(
            membership.LeagueId,
            membership.LeagueName,
            theirScore.BoostedPoints,
            membership.OverallRank,
            LeaderboardSnapshot.PlacesGained(membership.SnapshotOverallRank, membership.OverallRank),
            PlayerDisplayName.Format(topScorer.FirstName, topScorer.LastName),
            topScorer.BoostedPoints);
    }

    /// <summary>
    /// Who won the round in this league: the most points, and alphabetically by full name if two players tie.
    /// </summary>
    /// <remarks>
    /// The tie-break was on first name alone before, which cannot separate two players who share one, and disagreed
    /// with the tie-break every leaderboard on the site uses. Joint winners still yield a single name here, because the
    /// email has one line for it - but at least it is now the same name the league's own table puts at the top.
    /// </remarks>
    private static RoundLeagueScoreRow TopScorerOf(List<RoundLeagueScoreRow> leagueScores) =>
        Ranking.ByDescending(
                leagueScores,
                score => score.BoostedPoints,
                score => PlayerDisplayName.FormatFull(score.FirstName, score.LastName))
            .First()
            .Item;

    private static UserRoundDigest Digest(
        RoundDigestPlayerRow player,
        RoundDigestRoundRow round,
        RoundDigestRoundRow? nextRound,
        List<LeagueRoundDigest> leagues) =>
        new(player.UserId,
            player.Email,
            player.FirstName,
            Round.DisplayNameOrDefault(round.DisplayName, round.RoundNumber),
            player.ExactScoreCount,
            player.CorrectResultCount,
            nextRound is null ? null : Round.DisplayNameOrDefault(nextRound.DisplayName, nextRound.RoundNumber),
            nextRound?.DeadlineUtc,
            leagues);
}

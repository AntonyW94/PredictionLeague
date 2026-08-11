using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// The league dashboard's round grid: every member down the side, every fixture across the top, and each
/// member's position for the round.
///
/// The one leaderboard whose rules are about secrecy rather than arithmetic. Alongside the tie policy and the
/// display name it carried the rule that decides whether a player sees an opponent's prediction, which is the
/// difference between a prediction game and a copying game.
/// </summary>
public class GetLeagueDashboardRoundResultsQueryHandler(
    ILeagueRoundResultsQuery resultsQuery,
    ILeagueMembershipService membershipService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetLeagueDashboardRoundResultsQuery, IEnumerable<PredictionResultDto>>
{
    public async Task<IEnumerable<PredictionResultDto>> Handle(
        GetLeagueDashboardRoundResultsQuery request,
        CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await resultsQuery.ExecuteAsync(request.LeagueId, request.RoundId, cancellationToken);

        if (data == null)
            return [];

        var fixtures = data.Round.Matches
            .Where(match => !match.IsPostponed)
            .OrderBy(match => match.MatchDateTimeUtc)
            .ToList();

        var utcNow = dateTimeProvider.UtcNow;

        var pointsByUserId = data.Points.ToDictionary(row => row.UserId, row => row.BoostedPoints);
        var boostByUserId = BoostsByUserId(data.BoostUsages);
        var predictionsByUserId = data.Predictions
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.MatchId));

        var ranked = Ranking.ByDescending(
            data.Members,
            member => PointsFor(pointsByUserId, member.UserId),
            member => PlayerDisplayName.FormatFull(member.FirstName, member.LastName));

        return ranked
            .Select(entry => new PredictionResultDto
            {
                UserId = entry.Item.UserId,
                PlayerName = PlayerDisplayName.Format(entry.Item.FirstName, entry.Item.LastName),
                HasPredicted = HasAnyPrediction(predictionsByUserId, entry.Item.UserId),
                TotalPoints = PointsFor(pointsByUserId, entry.Item.UserId),
                Rank = entry.Rank,
                Predictions = CellsFor(entry.Item.UserId, fixtures, predictionsByUserId, data.Round, request.CurrentUserId, utcNow),
                AppliedBoostCode = boostByUserId.GetValueOrDefault(entry.Item.UserId)?.Code,
                AppliedBoostImageUrl = boostByUserId.GetValueOrDefault(entry.Item.UserId)?.ImageUrl
            })
            .ToList();
    }

    /// <summary>
    /// One cell per fixture for one member, whether or not they predicted it.
    /// </summary>
    /// <remarks>
    /// The grid is dense on purpose: a member who predicted three of ten fixtures still gets ten cells, so the
    /// columns line up across the rows. The old query manufactured those cells with a <c>CROSS JOIN</c> between
    /// members and fixtures and then papered over the missing predictions with <c>ISNULL(up.[Outcome], 0)</c>.
    /// A member with no predictions at all is the case to watch - joining rather than filling would drop their
    /// row from the grid entirely rather than showing them as having predicted nothing.
    /// </remarks>
    private static List<PredictionScoreDto> CellsFor(
        string userId,
        IReadOnlyList<Match> fixtures,
        IReadOnlyDictionary<string, Dictionary<int, MemberPredictionRow>> predictionsByUserId,
        Round round,
        string currentUserId,
        DateTime utcNow)
    {
        var cells = new List<PredictionScoreDto>(fixtures.Count);

        foreach (var fixture in fixtures)
        {
            var isVisible = PredictionVisibility.IsVisibleTo(fixture, userId, currentUserId, utcNow, round.DeadlineUtc);

            var prediction = FindPrediction(predictionsByUserId, userId, fixture.Id);

            cells.Add(new PredictionScoreDto(
                fixture.Id,
                prediction?.PredictedHomeScore,
                prediction?.PredictedAwayScore,
                prediction?.Outcome ?? PredictionOutcome.Pending,
                !isVisible));
        }

        return cells;
    }

    private static MemberPredictionRow? FindPrediction(
        IReadOnlyDictionary<string, Dictionary<int, MemberPredictionRow>> predictionsByUserId,
        string userId,
        int matchId)
    {
        if (!predictionsByUserId.TryGetValue(userId, out var byMatchId))
            return null;

        return byMatchId.GetValueOrDefault(matchId);
    }

    private static bool HasAnyPrediction(
        IReadOnlyDictionary<string, Dictionary<int, MemberPredictionRow>> predictionsByUserId,
        string userId)
    {
        if (!predictionsByUserId.TryGetValue(userId, out var byMatchId))
            return false;

        return byMatchId.Values.Any(prediction => prediction.PredictedHomeScore.HasValue);
    }

    /// <summary>
    /// The boost a member played this round. Nothing in the schema stops there being two, so the earliest by
    /// code wins - a stated rule rather than the old query's first non-empty row, which was whatever order the
    /// join happened to produce.
    /// </summary>
    private static Dictionary<string, MemberBoostUsageRow> BoostsByUserId(IReadOnlyList<MemberBoostUsageRow> usages) =>
        usages
            .GroupBy(usage => usage.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(usage => usage.Code, StringComparer.Ordinal).First());

    /// <summary>
    /// A member with no result row for the round scores zero rather than dropping off the grid, which is what
    /// the old <c>COALESCE(lrr.[BoostedPoints], 0)</c> was for.
    /// </summary>
    private static int PointsFor(IReadOnlyDictionary<string, int> pointsByUserId, string userId) =>
        pointsByUserId.TryGetValue(userId, out var points) ? points : 0;
}

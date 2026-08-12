using MediatR;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Predictions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Predictions.Queries;

/// <summary>
/// The prediction form for one round: its fixtures, whatever the player has entered so far, and the leagues their entry
/// counts towards.
/// </summary>
public class GetPredictionPageDataQueryHandler(
    IRoundHeaderQuery roundHeaderQuery,
    IRoundMatchesQuery roundMatchesQuery,
    IUserRoundPredictionsQuery userRoundPredictionsQuery,
    IPredictionLeaguesQuery predictionLeaguesQuery) : IRequestHandler<GetPredictionPageDataQuery, PredictionPageDto?>
{
    public async Task<PredictionPageDto?> Handle(GetPredictionPageDataQuery request, CancellationToken cancellationToken)
    {
        var round = await roundHeaderQuery.ExecuteAsync(request.RoundId, cancellationToken);

        if (round is null)
            return null;

        var matches = await roundMatchesQuery.ExecuteAsync(request.RoundId, cancellationToken);
        var predictions = await userRoundPredictionsQuery.ExecuteAsync(request.UserId, request.RoundId, cancellationToken);
        var leagues = await predictionLeaguesQuery.ExecuteAsync(request.UserId, round.SeasonId, cancellationToken);

        var predictionsByMatch = predictions.ToDictionary(prediction => prediction.MatchId);

        return new PredictionPageDto
        {
            RoundId = round.RoundId,
            RoundNumber = round.RoundNumber,
            RoundName = Round.DisplayNameOrDefault(round.DisplayName, round.RoundNumber),
            SeasonName = round.SeasonName,
            DeadlineUtc = round.DeadlineUtc,
            IsTournament = round.CompetitionType == CompetitionType.Tournament,

            // The last round of the season, which the page uses to say so rather than to change anything.
            IsLastRoundOfSeason = round.RoundNumber == round.NumberOfRounds,

            // A called-off fixture cannot be predicted, so it is not on the form.
            Matches = RoundMatches.InKickOffOrder(matches.Where(match => !RoundMatches.IsPostponed(match)))
                .Select(match => ToMatchDto(match, predictionsByMatch.GetValueOrDefault(match.Id)))
                .ToList(),

            Leagues = LeaguesFor(leagues, request.RoundId)
        };
    }

    private static MatchPredictionDto ToMatchDto(RoundMatchRow match, UserRoundPredictionRow? prediction) =>
        new()
        {
            MatchId = match.Id,
            MatchDateTimeUtc = match.MatchDateTimeUtc,
            MatchNumber = match.MatchNumber,
            HomeTeamName = match.HomeTeamName,
            HomeTeamShortName = match.HomeTeamShortName,
            HomeTeamAbbreviation = match.HomeTeamAbbreviation,
            HomeTeamLogoUrl = match.HomeTeamLogoUrl,
            AwayTeamName = match.AwayTeamName,
            AwayTeamShortName = match.AwayTeamShortName,
            AwayTeamAbbreviation = match.AwayTeamAbbreviation,
            AwayTeamLogoUrl = match.AwayTeamLogoUrl,
            PlaceholderHomeName = match.PlaceholderHomeName,
            PlaceholderAwayName = match.PlaceholderAwayName,
            AreTeamsConfirmed = RoundMatches.AreTeamsConfirmed(match),
            CustomLockTimeUtc = match.CustomLockTimeUtc,
            PredictedHomeScore = prediction?.PredictedHomeScore,
            PredictedAwayScore = prediction?.PredictedAwayScore
        };

    /// <summary>
    /// The leagues this entry counts towards, and what the player can still do with a boost in each.
    /// </summary>
    private static List<PredictionLeagueDto> LeaguesFor(PredictionLeaguesData data, int roundId) =>
        data.Leagues
            .OrderBy(league => league.Name, StringComparer.InvariantCultureIgnoreCase)
            .Select(league => new PredictionLeagueDto
            {
                LeagueId = league.LeagueId,
                Name = league.Name,
                HasBoosts = HasBoosts(data, league.LeagueId),
                HasUnusedBoostThisSeason = HasUnusedBoostThisSeason(data, league.LeagueId),
                SelectedBoostCode = SelectedBoostCode(data, league.LeagueId, roundId)
            })
            .ToList();

    /// <summary>
    /// Whether the league runs boosts at all, which is what decides if the page offers them.
    /// </summary>
    /// <remarks>
    /// A league can have rules recorded and every one of them switched off, which is not the same as running boosts. Disabled
    /// rules therefore have to arrive so this can tell the difference.
    /// </remarks>
    private static bool HasBoosts(PredictionLeaguesData data, int leagueId) =>
        data.BoostRules.Any(rule => rule.LeagueId == leagueId && rule.IsEnabled);

    /// <summary>
    /// Whether the player still has a boost to spend in this league this season.
    /// </summary>
    /// <remarks>
    /// A boost is available when its rule is switched on, allows at least one use in the season, and the player has not used
    /// that particular boost yet. Per boost rather than per league: having spent the double-points boost says nothing about
    /// whether the banker is still there. This was a <c>NOT EXISTS</c> nested inside an <c>EXISTS</c>.
    /// </remarks>
    private static bool HasUnusedBoostThisSeason(PredictionLeaguesData data, int leagueId) =>
        data.BoostRules.Any(rule =>
            rule.LeagueId == leagueId
            && rule.IsEnabled
            && rule.TotalUsesPerSeason > 0
            && !data.BoostUsages.Any(usage => usage.LeagueId == leagueId && usage.BoostDefinitionId == rule.BoostDefinitionId));

    /// <summary>The boost already picked for this round in this league, if there is one.</summary>
    private static string? SelectedBoostCode(PredictionLeaguesData data, int leagueId, int roundId) =>
        data.BoostUsages
            .FirstOrDefault(usage => usage.LeagueId == leagueId && usage.RoundId == roundId)
            ?.BoostCode;
}

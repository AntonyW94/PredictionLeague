using MediatR;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Features.Sharing.Models;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Sharing.Queries;

/// <summary>
/// A player's round of predictions, drawn as an image they can share.
/// </summary>
public class GetRoundShareCardImageQueryHandler(
    IRoundHeaderQuery roundHeaderQuery,
    IRoundMatchesQuery roundMatchesQuery,
    IUserRoundPredictionsQuery userRoundPredictionsQuery,
    IShareCardPlayerQuery shareCardPlayerQuery,
    IShareCardRenderer renderer) : IRequestHandler<GetRoundShareCardImageQuery, byte[]?>
{
    public async Task<byte[]?> Handle(GetRoundShareCardImageQuery request, CancellationToken cancellationToken)
    {
        var round = await roundHeaderQuery.ExecuteAsync(request.RoundId, cancellationToken);

        if (round is null)
            return null;

        var matches = await roundMatchesQuery.ExecuteAsync(request.RoundId, cancellationToken);
        var predictions = await userRoundPredictionsQuery.ExecuteAsync(request.UserId, request.RoundId, cancellationToken);

        var predictionsByMatch = predictions.ToDictionary(prediction => prediction.MatchId);

        var cardMatches = RoundMatches.InKickOffOrder(matches)
            .Select(match => ToCardMatch(match, predictionsByMatch.GetValueOrDefault(match.Id)))
            .OfType<ShareCardMatch>()
            .ToList();

        // Nothing to draw for a player who did not predict this round, so there is no card rather than an empty one.
        if (cardMatches.Count == 0)
            return null;

        var player = await shareCardPlayerQuery.ExecuteAsync(request.UserId, cancellationToken);

        var model = new ShareCardModel(
            PlayerName(player),
            round.SeasonName,
            RoundLabel(round),
            cardMatches,
            Theme(request.Theme, player));

        return await renderer.RenderAsync(model, cancellationToken);
    }

    /// <summary>
    /// One fixture on the card, or nothing if it does not belong on one.
    /// </summary>
    /// <remarks>
    /// Three reasons a fixture is left off: it was called off, its teams are not known yet - there is nothing to draw for
    /// "Winner of QF1" - or the player did not predict it. The card is a record of what somebody picked, so a fixture they
    /// left blank has no row.
    /// </remarks>
    private static ShareCardMatch? ToCardMatch(RoundMatchRow match, UserRoundPredictionRow? prediction)
    {
        if (RoundMatches.IsPostponed(match) || !RoundMatches.AreTeamsConfirmed(match))
            return null;

        if (prediction?.PredictedHomeScore is not { } predictedHome || prediction.PredictedAwayScore is not { } predictedAway)
            return null;

        return new ShareCardMatch(
            match.HomeTeamShortName!,
            match.HomeTeamAbbreviation!,
            match.HomeTeamLogoUrl,
            match.AwayTeamShortName!,
            match.AwayTeamAbbreviation!,
            match.AwayTeamLogoUrl,
            predictedHome,
            predictedAway,
            IsScored(match),
            match.ActualHomeTeamScore,
            match.ActualAwayTeamScore,
            prediction.Outcome);
    }

    /// <summary>
    /// Whether to show the real scoreline alongside the prediction, and colour the pick by how it did.
    /// </summary>
    /// <remarks>
    /// A scoreline exists and the fixture has at least kicked off. A scheduled fixture with scores recorded against it is a
    /// data error rather than a result, and drawing it would tell the player their prediction was wrong before it was played.
    /// </remarks>
    private static bool IsScored(RoundMatchRow match) =>
        match.ActualHomeTeamScore.HasValue
        && match.ActualAwayTeamScore.HasValue
        && match.Status is MatchStatus.InProgress or MatchStatus.Completed;

    /// <summary>
    /// What the card calls the round.
    /// </summary>
    /// <remarks>
    /// A tournament round is named - "Semi Finals" - and a league round is numbered. Deliberately narrower than
    /// <c>Round.DisplayNameOrDefault</c>, which uses the stored name whenever there is one: a league round is stored as
    /// "Gameweek 5" and this shows "Round 5". The two disagree on every league round in the database, and reconciling them is
    /// a product decision rather than a refactor - see the open questions in the plan.
    /// </remarks>
    private static string RoundLabel(RoundHeaderRow round) =>
        round.CompetitionType == CompetitionType.Tournament && !string.IsNullOrWhiteSpace(round.DisplayName)
            ? round.DisplayName
            : $"Round {round.RoundNumber}";

    /// <summary>The player's first name, or nothing to leave the card unnamed.</summary>
    private static string? PlayerName(ShareCardPlayerRow? player) =>
        string.IsNullOrWhiteSpace(player?.FirstName) ? null : player.FirstName;

    /// <summary>
    /// Which theme to draw.
    /// </summary>
    /// <remarks>
    /// The theme the browser is showing wins, because that is what the player is looking at; then their saved preference; then
    /// light, which is the application's default. Only an explicit "dark" produces the dark card, so an unrecognised value
    /// falls back rather than failing.
    /// </remarks>
    private static ShareCardTheme Theme(string? requestedTheme, ShareCardPlayerRow? player)
    {
        var themeValue = string.IsNullOrWhiteSpace(requestedTheme) ? player?.PreferredTheme : requestedTheme;

        return string.Equals(themeValue, "dark", StringComparison.OrdinalIgnoreCase)
            ? ShareCardTheme.Dark
            : ShareCardTheme.Light;
    }
}

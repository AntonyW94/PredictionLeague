using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Sharing.Models;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Sharing.Queries;

public class GetRoundShareCardImageQueryHandler(
    IApplicationReadDbConnection dbConnection,
    IShareCardRenderer renderer)
    : IRequestHandler<GetRoundShareCardImageQuery, byte[]?>
{
    public async Task<byte[]?> Handle(GetRoundShareCardImageQuery request, CancellationToken cancellationToken)
    {
        const string roundSql = @"
            SELECT
                r.[RoundNumber],
                r.[DisplayName] AS RoundDisplayName,
                s.[Name] AS SeasonName,
                c.[Type] AS CompetitionType,
                u.[FirstName] AS PlayerFirstName,
                u.[PreferredTheme]
            FROM
                [Rounds] r
            JOIN
                [Seasons] s ON r.[SeasonId] = s.[Id]
            JOIN
                [Competitions] c ON s.[CompetitionId] = c.[Id]
            JOIN
                [AspNetUsers] u ON u.[Id] = @UserId
            WHERE
                r.[Id] = @RoundId";

        var round = await dbConnection.QuerySingleOrDefaultAsync<ShareCardRoundResult>(
            roundSql,
            cancellationToken,
            new { request.RoundId, request.UserId });

        if (round is null)
            return null;

        const string matchesSql = @"
            SELECT
                ht.[ShortName] AS HomeTeamShortName,
                ht.[Abbreviation] AS HomeTeamAbbreviation,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[ShortName] AS AwayTeamShortName,
                at.[Abbreviation] AS AwayTeamAbbreviation,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                up.[Outcome],
                m.[Status],
                m.[ActualHomeTeamScore] AS ActualHomeScore,
                m.[ActualAwayTeamScore] AS ActualAwayScore
            FROM
                [Matches] m
            JOIN
                [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            JOIN
                [Teams] at ON m.[AwayTeamId] = at.[Id]
            JOIN
                [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = @UserId
            WHERE
                m.[RoundId] = @RoundId
                AND m.[Status] <> @PostponedStatus
            ORDER BY
                m.[MatchDateTimeUtc],
                ht.[ShortName]";

        var matchRows = (await dbConnection.QueryAsync<ShareCardMatchResult>(
            matchesSql,
            cancellationToken,
            new { request.RoundId, request.UserId, PostponedStatus = nameof(MatchStatus.Postponed) })).ToList();

        var matches = matchRows
            .Where(m => m.PredictedHomeScore.HasValue && m.PredictedAwayScore.HasValue)
            .Select(m =>
            {
                var status = Enum.Parse<MatchStatus>(m.Status);
                var isScored = m.ActualHomeScore.HasValue
                    && m.ActualAwayScore.HasValue
                    && status is MatchStatus.InProgress or MatchStatus.Completed;

                return new ShareCardMatch(
                    m.HomeTeamShortName,
                    m.HomeTeamAbbreviation,
                    m.HomeTeamLogoUrl,
                    m.AwayTeamShortName,
                    m.AwayTeamAbbreviation,
                    m.AwayTeamLogoUrl,
                    m.PredictedHomeScore!.Value,
                    m.PredictedAwayScore!.Value,
                    isScored,
                    m.ActualHomeScore,
                    m.ActualAwayScore,
                    m.Outcome);
            })
            .ToList();

        if (matches.Count == 0)
            return null;

        var isTournament = round.CompetitionType == (int)CompetitionType.Tournament;
        var roundLabel = isTournament && !string.IsNullOrWhiteSpace(round.RoundDisplayName)
            ? round.RoundDisplayName!
            : $"Round {round.RoundNumber}";

        var playerName = string.IsNullOrWhiteSpace(round.PlayerFirstName) ? null : round.PlayerFirstName;

        // The theme the client is showing wins; fall back to the saved preference, then to light
        // (the app's default). Only an explicit "dark" produces the dark card.
        var themeValue = string.IsNullOrWhiteSpace(request.Theme) ? round.PreferredTheme : request.Theme;
        var theme = string.Equals(themeValue, "dark", StringComparison.OrdinalIgnoreCase)
            ? ShareCardTheme.Dark
            : ShareCardTheme.Light;

        var model = new ShareCardModel(playerName, round.SeasonName, roundLabel, matches, theme);

        return await renderer.RenderAsync(model, cancellationToken);
    }

    // The row types are internal so a test can supply rows to the shaping above; InternalsVisibleTo
    // already exposes this assembly to ThePredictions.Application.Tests.Unit.
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    internal record ShareCardRoundResult(
        int RoundNumber,
        string? RoundDisplayName,
        string SeasonName,
        int CompetitionType,
        string? PlayerFirstName,
        string? PreferredTheme);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    internal record ShareCardMatchResult(
        string HomeTeamShortName,
        string HomeTeamAbbreviation,
        string? HomeTeamLogoUrl,
        string AwayTeamShortName,
        string AwayTeamAbbreviation,
        string? AwayTeamLogoUrl,
        int? PredictedHomeScore,
        int? PredictedAwayScore,
        PredictionOutcome Outcome,
        string Status,
        int? ActualHomeScore,
        int? ActualAwayScore);
}

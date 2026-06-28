using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Predictions;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Predictions.Queries;

public class GetPredictionPageDataQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetPredictionPageDataQuery, PredictionPageDto?>
{
    public async Task<PredictionPageDto?> Handle(GetPredictionPageDataQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id] AS RoundId,
                r.[RoundNumber],
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                c.[Type] AS CompetitionType,
                s.[NumberOfRounds],
                r.[DeadlineUtc],
                m.[Id] AS MatchId,
                m.[MatchDateTimeUtc],
                m.[MatchNumber],
                m.[HomeTeamId],
                ht.[Name] AS HomeTeamName,
                ht.[ShortName] AS HomeTeamShortName,
                ht.[Abbreviation] AS HomeTeamAbbreviation,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                m.[AwayTeamId],
                at.[Name] AS AwayTeamName,
                at.[ShortName] AS AwayTeamShortName,
                at.[Abbreviation] AS AwayTeamAbbreviation,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                m.[PlaceholderHomeName],
                m.[PlaceholderAwayName],
                m.[CustomLockTimeUtc],
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                r.[DisplayName] AS RoundDisplayName
            FROM [Rounds] r
            JOIN [Seasons] s ON r.[SeasonId] = s.[Id]
            JOIN [Competitions] c ON s.[CompetitionId] = c.[Id]
            LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]
            LEFT JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [UserPredictions] up ON m.[Id] = up.[MatchId] AND up.[UserId] = @UserId
            WHERE r.[Id] = @RoundId
                AND (m.[Status] IS NULL OR m.[Status] <> @PostponedStatus)
            ORDER BY m.[MatchDateTimeUtc], ht.[ShortName];";

        var queryResult = await dbConnection.QueryAsync<PredictionPageQueryResult>(
            sql,
            cancellationToken,
            new
            {
                request.UserId,
                request.RoundId,
                PostponedStatus = nameof(MatchStatus.Postponed)
            }
        );

        var results = queryResult.ToList();
        if (!results.Any())
            return null;

        var firstRow = results.First();
        var isTournament = firstRow.CompetitionType == (int)CompetitionType.Tournament;

        const string leaguesSql = @"
            SELECT
                l.[Id] AS LeagueId,
                l.[Name],
                CAST
                    (
                        CASE WHEN EXISTS (
                        SELECT 1
                        FROM [LeagueBoostRules] lbr
                        WHERE
                            lbr.[LeagueId] = l.[Id]
                            AND lbr.[IsEnabled] = 1
                        ) THEN 1 ELSE 0 END AS BIT
                    ) AS HasBoosts,
                CAST
                    (
                        CASE WHEN EXISTS (
                            SELECT 1
                            FROM [LeagueBoostRules] lbr
                            WHERE
                                lbr.[LeagueId] = l.[Id]
                                AND lbr.[IsEnabled] = 1
                                AND lbr.[TotalUsesPerSeason] > 0
                                AND NOT EXISTS (
                                    SELECT 1
                                    FROM [UserBoostUsages] ubu
                                    WHERE ubu.[UserId] = @UserId
                                        AND ubu.[LeagueId] = l.[Id]
                                        AND ubu.[SeasonId] = @SeasonId
                                        AND ubu.[BoostDefinitionId] = lbr.[BoostDefinitionId]
                                )
                        ) THEN 1 ELSE 0 END AS BIT
                    ) AS HasUnusedBoostThisSeason
            FROM
                [Leagues] l
            JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = l.[Id]
            WHERE
                l.[SeasonId] = @SeasonId
                AND lm.[UserId] = @UserId
                AND lm.[Status] = @ApprovedStatus
            ORDER BY
                l.[Name];";

        var leagues = await dbConnection.QueryAsync<PredictionLeagueQueryResult>(
            leaguesSql,
            cancellationToken,
            new { firstRow.SeasonId, request.UserId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) }
        );

        const string userBoostSql = @"
            SELECT
                ubu.[LeagueId],
                bd.[Code] AS SelectedBoostCode
            FROM [UserBoostUsages] ubu
            JOIN [BoostDefinitions] bd ON bd.[Id] = ubu.[BoostDefinitionId]
            WHERE
                ubu.[UserId] = @UserId
                AND ubu.[RoundId] = @RoundId;";

        var boostUsages = await dbConnection.QueryAsync<UserBoostUsageResult>(
            userBoostSql,
            cancellationToken,
            new { request.UserId, request.RoundId }
        );

        var boostDictionary = boostUsages.ToDictionary(x => x.LeagueId, x => x.SelectedBoostCode);

        return new PredictionPageDto
        {
            RoundId = firstRow.RoundId,
            RoundNumber = firstRow.RoundNumber,
            RoundDisplayName = firstRow.RoundDisplayName,
            SeasonName = firstRow.SeasonName,
            DeadlineUtc = firstRow.DeadlineUtc,
            IsPastDeadline = firstRow.DeadlineUtc < DateTime.UtcNow,
            IsTournament = isTournament,
            IsLastRoundOfSeason = firstRow.RoundNumber == firstRow.NumberOfRounds,
            Matches = results
                .Where(r => r.MatchId.HasValue)
                .Select(r =>
                {
                    var teamsConfirmed = r.HomeTeamId.HasValue && r.AwayTeamId.HasValue;
                    return new MatchPredictionDto
                    {
                        MatchId = r.MatchId!.Value,
                        MatchDateTimeUtc = r.MatchDateTimeUtc!.Value,
                        MatchNumber = r.MatchNumber,
                        HomeTeamName = r.HomeTeamName,
                        HomeTeamShortName = r.HomeTeamShortName,
                        HomeTeamAbbreviation = r.HomeTeamAbbreviation,
                        HomeTeamLogoUrl = r.HomeTeamLogoUrl,
                        AwayTeamName = r.AwayTeamName,
                        AwayTeamShortName = r.AwayTeamShortName,
                        AwayTeamAbbreviation = r.AwayTeamAbbreviation,
                        AwayTeamLogoUrl = r.AwayTeamLogoUrl,
                        PlaceholderHomeName = r.PlaceholderHomeName,
                        PlaceholderAwayName = r.PlaceholderAwayName,
                        AreTeamsConfirmed = teamsConfirmed,
                        CustomLockTimeUtc = r.CustomLockTimeUtc,
                        PredictedHomeScore = r.PredictedHomeScore,
                        PredictedAwayScore = r.PredictedAwayScore
                    };
                }).ToList(),
            Leagues = leagues
                .Select(l => new PredictionLeagueDto
                {
                    LeagueId = l.LeagueId,
                    Name = l.Name,
                    HasBoosts = l.HasBoosts,
                    HasUnusedBoostThisSeason = l.HasUnusedBoostThisSeason,
                    SelectedBoostCode = boostDictionary.GetValueOrDefault(l.LeagueId)
                }).ToList()
        };
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PredictionPageQueryResult(
        int RoundId,
        int RoundNumber,
        int SeasonId,
        string SeasonName,
        int CompetitionType,
        int NumberOfRounds,
        DateTime DeadlineUtc,
        int? MatchId,
        DateTime? MatchDateTimeUtc,
        int? MatchNumber,
        int? HomeTeamId,
        string? HomeTeamName,
        string? HomeTeamShortName,
        string? HomeTeamAbbreviation,
        string? HomeTeamLogoUrl,
        int? AwayTeamId,
        string? AwayTeamName,
        string? AwayTeamShortName,
        string? AwayTeamAbbreviation,
        string? AwayTeamLogoUrl,
        string? PlaceholderHomeName,
        string? PlaceholderAwayName,
        DateTime? CustomLockTimeUtc,
        int? PredictedHomeScore,
        int? PredictedAwayScore,
        string? RoundDisplayName
    );

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PredictionLeagueQueryResult(int LeagueId, string Name, bool HasBoosts, bool HasUnusedBoostThisSeason);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UserBoostUsageResult(int LeagueId, string SelectedBoostCode);
}

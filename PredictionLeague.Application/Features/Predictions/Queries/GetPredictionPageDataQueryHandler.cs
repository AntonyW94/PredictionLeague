using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Predictions;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Predictions.Queries;

public class GetPredictionPageDataQueryHandler : IRequestHandler<GetPredictionPageDataQuery, PredictionPageDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetPredictionPageDataQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<PredictionPageDto?> Handle(GetPredictionPageDataQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id] AS RoundId,
                r.[RoundNumber],
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                r.[DeadlineUtc],
                m.[Id] AS MatchId,
                m.[MatchDateTimeUtc],
                ht.[Name] AS HomeTeamName,
                ht.[ShortName] AS HomeTeamShortName,
                ht.[Abbreviation] AS HomeTeamAbbreviation,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[Name] AS AwayTeamName,
                at.[ShortName] AS AwayTeamShortName,
                at.[Abbreviation] AS AwayTeamAbbreviation, 
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore]
            FROM [Rounds] r
            JOIN [Seasons] s ON r.[SeasonId] = s.[Id]
            LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]
            LEFT JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [UserPredictions] up ON m.[Id] = up.[MatchId] AND up.[UserId] = @UserId
            WHERE r.[Id] = @RoundId
            ORDER BY m.[MatchDateTimeUtc];";

        var queryResult = await _dbConnection.QueryAsync<PredictionPageQueryResult>(
            sql,
            cancellationToken,
            new
            {
                request.RoundId, 
                request.UserId
            }
        );

        var results = queryResult.ToList();
        if (!results.Any())
            return null;

        var firstRow = results.First();

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
                    ) AS HasBoosts
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

        var leagues = await _dbConnection.QueryAsync<PredictionLeagueQueryResult>(
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

        var boostUsages = await _dbConnection.QueryAsync<UserBoostUsageResult>(
            userBoostSql,
            cancellationToken,
            new { request.UserId, request.RoundId }
        );
        
        var boostDictionary = boostUsages.ToDictionary(x => x.LeagueId, x => x.SelectedBoostCode);

        return new PredictionPageDto
        {
            RoundId = firstRow.RoundId,
            RoundNumber = firstRow.RoundNumber,
            SeasonName = firstRow.SeasonName,
            DeadlineUtc = firstRow.DeadlineUtc,
            IsPastDeadline = firstRow.DeadlineUtc < DateTime.UtcNow,
            Matches = results
                .Where(r => r.MatchId.HasValue)
                .Select(r => new MatchPredictionDto
                {
                    MatchId = r.MatchId!.Value,
                    MatchDateTimeUtc = r.MatchDateTimeUtc!.Value,
                    HomeTeamName = r.HomeTeamName,
                    HomeTeamShortName = r.HomeTeamShortName,
                    HomeTeamAbbreviation = r.HomeTeamAbbreviation,
                    HomeTeamLogoUrl = r.HomeTeamLogoUrl,
                    AwayTeamName = r.AwayTeamName,
                    AwayTeamShortName = r.AwayTeamShortName,
                    AwayTeamAbbreviation = r.AwayTeamAbbreviation,
                    AwayTeamLogoUrl = r.AwayTeamLogoUrl,
                    PredictedHomeScore = r.PredictedHomeScore,
                    PredictedAwayScore = r.PredictedAwayScore
                }).ToList(),
            Leagues = leagues
                .Select(l => new PredictionLeagueDto
                {
                    LeagueId = l.LeagueId, 
                    Name = l.Name,
                    HasBoosts = l.HasBoosts,
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
        DateTime DeadlineUtc,
        int? MatchId,
        DateTime? MatchDateTimeUtc,
        string HomeTeamName,
        string HomeTeamShortName,
        string HomeTeamAbbreviation,
        string HomeTeamLogoUrl,
        string AwayTeamName,
        string AwayTeamShortName,
        string AwayTeamAbbreviation,
        string AwayTeamLogoUrl,
        int? PredictedHomeScore,
        int? PredictedAwayScore
    );

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PredictionLeagueQueryResult(int LeagueId, string Name, bool HasBoosts);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UserBoostUsageResult(int LeagueId, string SelectedBoostCode);
}

using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueDashboardRoundResultsQueryHandler : IRequestHandler<GetLeagueDashboardRoundResultsQuery, IEnumerable<PredictionResultDto>?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetLeagueDashboardRoundResultsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<PredictionResultDto>?> Handle(GetLeagueDashboardRoundResultsQuery request, CancellationToken cancellationToken)
    {
        const string roundStatusSql = "SELECT [Status] FROM [Rounds] WHERE [Id] = @RoundId;";
      
        var roundStatus = await _dbConnection.QuerySingleOrDefaultAsync<string>(roundStatusSql, cancellationToken, new { request.RoundId });
        if (roundStatus == null || Enum.Parse<RoundStatus>(roundStatus) == RoundStatus.Draft)
            return null;

        const string sql = @"WITH RoundRankings AS (
                                SELECT 
                                    lm.[UserId],
                                    COALESCE(lrr.[BoostedPoints], 0) AS [TotalPoints],
                                    RANK() OVER (ORDER BY COALESCE(lrr.[BoostedPoints], 0) DESC) AS [Rank]
                                FROM 
                                    [LeagueMembers] lm
                                LEFT JOIN 
                                    [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = @LeagueId AND lrr.[RoundId] = @RoundId
                                WHERE 
                                    lm.[LeagueId] = @LeagueId 
                                    AND lm.[Status] = @Approved
                            )

                            SELECT
                                lm.[UserId],
                                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [PlayerName],
                                
                                m.[Id] AS [MatchId],
                                
                                up.[PredictedHomeScore],
                                up.[PredictedAwayScore],
                                ISNULL(up.[Outcome], 0) AS [Outcome],
                                
                                CAST(CASE 
                                    WHEN r.[Deadline] > GETDATE() AND lm.[UserId] != @CurrentUserId THEN 1 
                                    ELSE 0 
                                END AS bit) AS [IsHidden],

                                rr.[Rank],
                                rr.[TotalPoints],
                                bd.[Code] AS AppliedBoostCode,
                                bd.[ImageUrl] AS AppliedBoostImageUrl

                            FROM [LeagueMembers] lm
                            JOIN [AspNetUsers] u ON lm.[UserId] = u.[Id]
                            JOIN [RoundRankings] rr ON rr.[UserId] = lm.[UserId]
                            JOIN [Rounds] r ON r.[Id] = @RoundId
                            CROSS JOIN [Matches] m
                            LEFT JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = lm.[UserId]
                            LEFT JOIN [UserBoostUsages] ubu ON ubu.[UserId] = lm.[UserId] 
                                                            AND ubu.[RoundId] = @RoundId 
                                                            AND ubu.[LeagueId] = @LeagueId
                            LEFT JOIN [BoostDefinitions] bd ON bd.[Id] = ubu.[BoostDefinitionId]
                            WHERE 
                                lm.[LeagueId] = @LeagueId 
                                AND lm.[Status] = @Approved
                                AND m.[RoundId] = @RoundId
                            ORDER BY 
                                rr.[Rank], 
                                PlayerName, 
                                m.[MatchDateTime];";

        var parameters = new
        {
            request.LeagueId, 
            request.RoundId, 
            request.CurrentUserId,
            Approved = nameof(LeagueMemberStatus.Approved)
        };

        var queryResult = await _dbConnection.QueryAsync<PredictionQueryResult>(sql, cancellationToken, parameters);

        var groupedResults = queryResult
             .GroupBy(r => new { r.UserId, r.PlayerName, r.Rank, r.TotalPoints })
             .Select(g =>
             {
                 var boostCode = g.Select(x => x.AppliedBoostCode).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                 var boostImage = g.Select(x => x.AppliedBoostImageUrl).FirstOrDefault(x => !string.IsNullOrEmpty(x));

                 var predictions = g.Select(p =>
                 {
                     var scoreDto = new PredictionScoreDto(
                         p.MatchId,
                         p.PredictedHomeScore,
                         p.PredictedAwayScore,
                         p.Outcome,
                         p.IsHidden
                     );

                     return scoreDto;
                 }).ToList();

                 return new PredictionResultDto
                 {
                     UserId = g.Key.UserId,
                     PlayerName = g.Key.PlayerName,
                     TotalPoints = g.Key.TotalPoints,
                     Rank = g.Key.Rank,
                     HasPredicted = g.Any(p => p.PredictedHomeScore.HasValue),
                     Predictions = predictions,
                     AppliedBoostCode = boostCode,
                     AppliedBoostImageUrl = boostImage
                 };
             });

        return groupedResults;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PredictionQueryResult(
        string UserId,
        string PlayerName,
        int MatchId,
        int? PredictedHomeScore,
        int? PredictedAwayScore,
        PredictionOutcome Outcome,
        bool IsHidden,
        long Rank,
        int TotalPoints,
        string? AppliedBoostCode,
        string? AppliedBoostImageUrl
    );
}
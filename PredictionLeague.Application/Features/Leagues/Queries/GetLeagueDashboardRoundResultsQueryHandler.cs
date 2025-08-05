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

        const string sql = @"
            SELECT
                lm.[UserId],
                u.[FirstName] + ' ' + u.[LastName] AS PlayerName,
                m.[Id] AS MatchId,
                up.[PointsAwarded],
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                CAST(CASE 
                    WHEN r.[Deadline] > GETUTCDATE() AND up.[UserId] != @CurrentUserId THEN 1 
                    ELSE 0 
                END AS bit) AS IsHidden
            FROM 
                [LeagueMembers] lm
            JOIN [AspNetUsers] u ON lm.[UserId] = u.[Id]
            JOIN [Rounds] r ON r.[Id] = @RoundId
            CROSS JOIN [Matches] m
            LEFT JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = lm.[UserId]
            WHERE 
                lm.[LeagueId] = @LeagueId 
                AND lm.[Status] = @Approved
                AND m.[RoundId] = @RoundId
            ORDER BY 
                u.[FirstName], u.[LastName], m.[MatchDateTime];";

        var parameters = new
        {
            request.LeagueId, 
            request.RoundId, 
            request.CurrentUserId,
            Approved = nameof(LeagueMemberStatus.Approved)
        };

        var queryResult = await _dbConnection.QueryAsync<PredictionQueryResult>(sql, cancellationToken, parameters);

        var groupedResults = queryResult
            .GroupBy(r => new { r.UserId, r.PlayerName })
            .Select(g => new PredictionResultDto
            {
                UserId = g.Key.UserId,
                PlayerName = g.Key.PlayerName,
                TotalPoints = g.Sum(p => p.PointsAwarded ?? 0),
                Predictions = g.Select(p => new PredictionScoreDto(
                    p.MatchId,
                    p.PredictedHomeScore,
                    p.PredictedAwayScore,
                    p.PointsAwarded,
                    p.IsHidden
                )).ToList()
            });

        return groupedResults;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PredictionQueryResult(
        string UserId,
        string PlayerName,
        int MatchId,
        int? PointsAwarded,
        int? PredictedHomeScore,
        int? PredictedAwayScore, 
        bool IsHidden
    );
}
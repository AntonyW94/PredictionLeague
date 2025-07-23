using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Predictions;
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
                s.[Name] AS SeasonName,
                r.[Deadline],
                m.[Id] AS MatchId,
                m.[MatchDateTime],
                ht.[Name] AS HomeTeamName,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[Name] AS AwayTeamName,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore]
            FROM [dbo].[Rounds] r
            JOIN [dbo].[Seasons] s ON r.[SeasonId] = s.[Id]
            LEFT JOIN [dbo].[Matches] m ON r.[Id] = m.[RoundId]
            LEFT JOIN [dbo].[Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN [dbo].[Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [dbo].[UserPredictions] up ON m.[Id] = up.[MatchId] AND up.[UserId] = @UserId
            WHERE r.[Id] = @RoundId
            ORDER BY m.[MatchDateTime];";

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

        return new PredictionPageDto
        {
            RoundId = firstRow.RoundId,
            RoundNumber = firstRow.RoundNumber,
            SeasonName = firstRow.SeasonName,
            Deadline = firstRow.Deadline,
            IsPastDeadline = firstRow.Deadline < DateTime.UtcNow,
            Matches = results
                .Where(r => r.MatchId.HasValue)
                .Select(r => new MatchPredictionDto
                {
                    MatchId = r.MatchId!.Value,
                    MatchDateTime = r.MatchDateTime!.Value,
                    HomeTeamName = r.HomeTeamName!,
                    HomeTeamLogoUrl = r.HomeTeamLogoUrl,
                    AwayTeamName = r.AwayTeamName!,
                    AwayTeamLogoUrl = r.AwayTeamLogoUrl,
                    PredictedHomeScore = r.PredictedHomeScore,
                    PredictedAwayScore = r.PredictedAwayScore
                }).ToList()
        };
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PredictionPageQueryResult(
        int RoundId,
        int RoundNumber,
        string SeasonName,
        DateTime Deadline,
        int? MatchId,
        DateTime? MatchDateTime,
        string? HomeTeamName,
        string? HomeTeamLogoUrl,
        string? AwayTeamName,
        string? AwayTeamLogoUrl,
        int? PredictedHomeScore,
        int? PredictedAwayScore
    );
}

using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class FetchRoundsForSeasonQueryHandler : IRequestHandler<FetchRoundsForSeasonQuery, IEnumerable<RoundDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public FetchRoundsForSeasonQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<RoundDto>> Handle(FetchRoundsForSeasonQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id],
                r.[RoundNumber],
                r.[StartDate],
                r.[Deadline],
                COUNT(m.[Id]) AS MatchCount
            FROM [dbo].[Rounds] r
            LEFT JOIN [dbo].[Matches] m ON r.[Id] = m.[RoundId]
            WHERE r.[SeasonId] = @SeasonId
            GROUP BY r.[Id], r.[RoundNumber], r.[StartDate], r.[Deadline]
            ORDER BY r.[RoundNumber];";

        return await _dbConnection.QueryAsync<RoundDto>(sql, cancellationToken, new { request.SeasonId });
    }
}
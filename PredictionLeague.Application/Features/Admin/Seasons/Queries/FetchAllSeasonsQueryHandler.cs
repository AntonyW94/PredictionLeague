using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Queries;

public class FetchAllSeasonsQueryHandler : IRequestHandler<FetchAllSeasonsQuery, IEnumerable<SeasonDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public FetchAllSeasonsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<SeasonDto>> Handle(FetchAllSeasonsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                s.[StartDate],
                s.[EndDate],
                s.[IsActive],
                COUNT(r.[Id]) AS RoundCount
            FROM [dbo].[Seasons] s
            LEFT JOIN [dbo].[Rounds] r ON s.[Id] = r.[SeasonId]
            GROUP BY s.[Id], s.[Name], s.[StartDate], s.[EndDate], s.[IsActive]
            ORDER BY s.[StartDate] DESC;";

        return await _dbConnection.QueryAsync<SeasonDto>(sql, cancellationToken);
    }
}
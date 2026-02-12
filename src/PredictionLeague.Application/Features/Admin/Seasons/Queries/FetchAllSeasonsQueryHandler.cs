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
                s.[StartDateUtc],
                s.[EndDateUtc],
                s.[IsActive],
                s.[NumberOfRounds],
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id]) as RoundCount
            FROM 
                [Seasons] s
            ORDER BY 
                s.[StartDateUtc] DESC;";

        return await _dbConnection.QueryAsync<SeasonDto>(sql, cancellationToken);
    }
}
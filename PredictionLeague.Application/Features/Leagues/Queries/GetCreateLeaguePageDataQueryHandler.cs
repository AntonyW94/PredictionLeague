using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Seasons;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetCreateLeaguePageDataQueryHandler : IRequestHandler<GetCreateLeaguePageDataQuery, CreateLeaguePageData>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetCreateLeaguePageDataQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<CreateLeaguePageData> Handle(GetCreateLeaguePageDataQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                s.[StartDateUtc]
            FROM [Seasons] s
            WHERE s.[IsActive] = 1
            ORDER BY s.[StartDateUtc] DESC;";

        var seasons = await _dbConnection.QueryAsync<SeasonLookupDto>(sql, cancellationToken);

        return new CreateLeaguePageData
        {
            Seasons = seasons.ToList()
        };
    }
}
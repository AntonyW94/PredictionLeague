using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Queries;

public class GetSeasonByIdQueryHandler : IRequestHandler<GetSeasonByIdQuery, SeasonDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetSeasonByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<SeasonDto?> Handle(GetSeasonByIdQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Name],
                s.[StartDate],
                s.[EndDate],
                s.[IsActive],
                COUNT(r.[Id]) AS RoundCount
            FROM [dbo].[Seasons] s
            LEFT JOIN [dbo].[Rounds] r ON s.[Id] = r.[SeasonId]
            WHERE s.[Id] = @Id
            GROUP BY s.[Id], s.[Name], s.[StartDate], s.[EndDate], s.[IsActive]";

        return await _dbConnection.QuerySingleOrDefaultAsync<SeasonDto>(sql, cancellationToken, new { request.Id });
    }
}
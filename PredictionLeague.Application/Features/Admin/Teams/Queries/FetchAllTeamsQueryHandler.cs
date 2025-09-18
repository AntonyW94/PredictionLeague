using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Queries;

public class FetchAllTeamsQueryHandler : IRequestHandler<FetchAllTeamsQuery, IEnumerable<TeamDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public FetchAllTeamsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<TeamDto>> Handle(FetchAllTeamsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = @"
            SELECT
                [Id],
                [Name],
                [ShortName],
                [LogoUrl],
                [Abbreviation],
                [ApiTeamId]
            FROM [Teams]
            ORDER BY [Name] ASC";

            return await _dbConnection.QueryAsync<TeamDto>(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while fetching teams from the database.", ex);
        }
    }
}
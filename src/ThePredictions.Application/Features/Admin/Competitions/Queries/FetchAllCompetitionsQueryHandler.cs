using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

public class FetchAllCompetitionsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<FetchAllCompetitionsQuery, IEnumerable<CompetitionDto>>
{
    public async Task<IEnumerable<CompetitionDto>> Handle(FetchAllCompetitionsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                c.[Id],
                c.[Code],
                c.[Name],
                c.[Type],
                c.[LogoUrl],
                c.[Description],
                c.[ApiLeagueId],
                (SELECT COUNT(*) FROM [Seasons] s WHERE s.[CompetitionId] = c.[Id]) AS SeasonCount
            FROM
                [Competitions] c
            ORDER BY
                c.[Name] ASC;";

        return await dbConnection.QueryAsync<CompetitionDto>(sql, cancellationToken);
    }
}

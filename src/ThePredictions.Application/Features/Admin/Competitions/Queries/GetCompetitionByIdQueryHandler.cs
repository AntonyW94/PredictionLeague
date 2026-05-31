using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

public class GetCompetitionByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetCompetitionByIdQuery, CompetitionDto?>
{
    public async Task<CompetitionDto?> Handle(GetCompetitionByIdQuery request, CancellationToken cancellationToken)
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
            WHERE
                c.[Id] = @Id;";

        return await dbConnection.QuerySingleOrDefaultAsync<CompetitionDto>(sql, cancellationToken, new { request.Id });
    }
}

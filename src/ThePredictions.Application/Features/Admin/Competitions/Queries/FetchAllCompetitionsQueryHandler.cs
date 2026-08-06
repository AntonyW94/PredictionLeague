using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
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

        var competitions = await dbConnection.QueryAsync<CompetitionQueryResult>(sql, cancellationToken);

        return competitions.Select(c => new CompetitionDto(
            c.Id,
            c.Code,
            c.Name,
            c.Type,
            c.LogoUrl,
            c.Description,
            c.ApiLeagueId,
            c.SeasonCount));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record CompetitionQueryResult(
        int Id,
        string Code,
        string Name,
        int Type,
        string? LogoUrl,
        string? Description,
        int? ApiLeagueId,
        int SeasonCount);
}

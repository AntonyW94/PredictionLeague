using ThePredictions.Domain.Common.Exceptions;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Teams;

namespace ThePredictions.Application.Features.Admin.Teams.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetTeamByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetTeamByIdQuery, TeamDto>
{
    public async Task<TeamDto> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
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
            WHERE [Id] = @Id";

        var team = await dbConnection.QuerySingleOrDefaultAsync<TeamQueryResult>(sql, cancellationToken, new { request.Id });

        if (team is null)
            throw new EntityNotFoundException("Team", request.Id);

        return new TeamDto(
            team.Id,
            team.Name,
            team.ShortName,
            team.LogoUrl,
            team.Abbreviation,
            team.ApiTeamId);
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record TeamQueryResult(
        int Id,
        string Name,
        string ShortName,
        string LogoUrl,
        string Abbreviation,
        int? ApiTeamId);
}

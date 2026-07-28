using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Teams;

namespace ThePredictions.Application.Features.Admin.Teams.Queries;

public class GetTeamByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetTeamByIdQuery, TeamDto?>
{
    public async Task<TeamDto?> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
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

        return team is null
            ? null
            : new TeamDto(
                team.Id,
                team.Name,
                team.ShortName,
                team.LogoUrl,
                team.Abbreviation,
                team.ApiTeamId);
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record TeamQueryResult(
        int Id,
        string Name,
        string ShortName,
        string LogoUrl,
        string Abbreviation,
        int? ApiTeamId);
}

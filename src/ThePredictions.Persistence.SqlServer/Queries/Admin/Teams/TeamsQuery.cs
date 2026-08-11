using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Teams.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Teams;

/// <summary>The SQL Server read behind <see cref="ITeamsQuery"/>. Its <c>ORDER BY [Name]</c> was a rule and has moved.</summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class TeamsQuery(IApplicationReadDbConnection dbConnection) : ITeamsQuery
{
    public async Task<IReadOnlyList<TeamRow>> ExecuteAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                t.[Id],
                t.[Name],
                t.[ShortName],
                t.[LogoUrl],
                t.[Abbreviation],
                t.[ApiTeamId]
            FROM
                [Teams] t;";

        return (await dbConnection.QueryAsync<TeamRow>(sql, cancellationToken)).ToList();
    }
}

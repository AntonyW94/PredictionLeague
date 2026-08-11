using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueMembersQuery"/>.
///
/// The league first, then its memberships - so whether the league exists no longer depends on whether anybody has
/// joined it, which is how the old handler decided.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueMembersQuery(IApplicationReadDbConnection dbConnection) : ILeagueMembersQuery
{
    public async Task<LeagueMembersData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var leagueName = await dbConnection.QuerySingleOrDefaultAsync<string>(
            @"
            SELECT
                l.[Name]
            FROM
                [Leagues] l
            WHERE
                l.[Id] = @LeagueId;",
            cancellationToken,
            new { LeagueId = leagueId });

        if (leagueName == null)
            return null;

        // Every membership, whatever its status, and in no particular order: this page is where an administrator
        // approves and rejects, and the order it reads in is a rule.
        const string membersSql = @"
            SELECT
                lm.[UserId],
                u.[FirstName],
                u.[LastName],
                lm.[JoinedAtUtc],
                lm.[Status]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            WHERE
                lm.[LeagueId] = @LeagueId;";

        var members = (await dbConnection.QueryAsync<LeagueMembershipRow>(
            membersSql, cancellationToken, new { LeagueId = leagueId })).ToList();

        return new LeagueMembersData(leagueName, members);
    }
}

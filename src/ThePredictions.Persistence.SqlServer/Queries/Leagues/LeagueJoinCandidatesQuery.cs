using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueJoinCandidatesQuery"/>.
///
/// The league first, so that a league nobody could be added to is still distinguishable from a league that does not
/// exist - the same reason <see cref="LeagueMembersQuery"/> reads in two steps.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueJoinCandidatesQuery(IApplicationReadDbConnection dbConnection) : ILeagueJoinCandidatesQuery
{
    private const string LeagueExistsSql = @"
        SELECT
            COUNT(*)
        FROM
            [Leagues] l
        WHERE
            l.[Id] = @LeagueId;";

    /// <summary>
    /// The league's season decides who is eligible, so the season is reached through the league rather than passed in -
    /// a caller that supplied its own season id could ask for the wrong one.
    /// </summary>
    private const string CandidatesSql = @"
        SELECT
            sp.[UserId],
            u.[FirstName],
            u.[LastName],
            u.[Email]
        FROM
            [SeasonPasses] sp
        INNER JOIN
            [Leagues] l ON l.[SeasonId] = sp.[SeasonId]
        INNER JOIN
            [AspNetUsers] u ON u.[Id] = sp.[UserId]
        WHERE
            l.[Id] = @LeagueId
            AND NOT EXISTS
            (
                SELECT
                    1
                FROM
                    [LeagueMembers] lm
                WHERE
                    lm.[LeagueId] = l.[Id]
                    AND lm.[UserId] = sp.[UserId]
            );";

    public async Task<IReadOnlyList<LeagueJoinCandidateRow>?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var leagueCount = await dbConnection.QuerySingleOrDefaultAsync<int>(
            LeagueExistsSql, cancellationToken, new { LeagueId = leagueId });

        if (leagueCount == 0)
            return null;

        return (await dbConnection.QueryAsync<LeagueJoinCandidateRow>(
            CandidatesSql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }
}

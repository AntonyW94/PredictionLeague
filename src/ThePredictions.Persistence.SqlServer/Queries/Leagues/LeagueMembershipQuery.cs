using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueMembershipQuery"/>. Two existence checks and nothing else - what
/// their answers mean is decided by <see cref="LeagueMembershipService"/> and its callers.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueMembershipQuery(IApplicationReadDbConnection dbConnection) : ILeagueMembershipQuery
{
    public async Task<bool> IsApprovedMemberAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                COUNT(*)
            FROM
                [LeagueMembers] lm
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[UserId] = @UserId
                AND lm.[Status] = @ApprovedStatus;";

        var count = await dbConnection.QuerySingleOrDefaultAsync<int>(
            sql, cancellationToken,
            new { LeagueId = leagueId, UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return count > 0;
    }

    public async Task<bool> IsAdministratorAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                COUNT(*)
            FROM
                [Leagues] l
            WHERE
                l.[Id] = @LeagueId
                AND l.[AdministratorUserId] = @UserId;";

        var count = await dbConnection.QuerySingleOrDefaultAsync<int>(
            sql, cancellationToken, new { LeagueId = leagueId, UserId = userId });

        return count > 0;
    }
}

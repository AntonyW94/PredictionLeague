using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Badges.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Badges;

/// <summary>
/// The SQL Server reads behind <see cref="IBadgeLeaderboardQuery"/>.
///
/// Two reads with no clause between them beyond the tables themselves. What is gone: the
/// <c>WHERE FirstName IS NOT NULL AND FirstName &lt;&gt; ''</c> that decided who is a player, the
/// <c>COUNT(DISTINCT BadgeKey)</c> that decided what a badge total is, the name abbreviation, and - from the
/// dashboard tile - a whole second statement that worked out one player's position and did not agree with this one.
/// </summary>
/// <remarks>
/// Both sides are small: every account, and every badge ever awarded. Bringing them together unjoined is what lets
/// one rule serve the table and the tile, which is the point of the change.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class BadgeLeaderboardQuery(IApplicationReadDbConnection dbConnection) : IBadgeLeaderboardQuery
{
    public async Task<BadgeLeaderboardData> ExecuteAsync(CancellationToken cancellationToken)
    {
        var players = await GetPlayersAsync(cancellationToken);
        var awards = await GetAwardsAsync(cancellationToken);

        return new BadgeLeaderboardData(players, awards);
    }

    private async Task<IReadOnlyList<BadgePlayerRow>> GetPlayersAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[FirstName],
                u.[LastName]
            FROM
                [AspNetUsers] u;";

        return (await dbConnection.QueryAsync<BadgePlayerRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<BadgePlayerAwardRow>> GetAwardsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ub.[UserId],
                ub.[BadgeKey],
                ub.[AwardedUtc]
            FROM
                [UserBadges] ub;";

        return (await dbConnection.QueryAsync<BadgePlayerAwardRow>(sql, cancellationToken)).ToList();
    }
}

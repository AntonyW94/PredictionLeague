using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Badges;

/// <summary>
/// The SQL Server reads behind <see cref="IBadgeStateQuery"/>.
///
/// Three reads, where there were six statements. What is gone: two gap-and-island streak queries, each four CTEs
/// deep, that worked out the longest and current runs of rounds with an exact score; a <c>TOP 1 ... ORDER BY
/// SeasonId DESC</c> that decided which season a player's totals were about, written out three separate times; the
/// ever-present arithmetic; and the abbreviation of the player's own name.
/// </summary>
/// <remarks>
/// Every round of every season comes back rather than a filtered set, because the badges disagree about which rounds
/// they care about and the read cannot serve one without breaking another. There are under a hundred rounds in the
/// database and one row each, so this is smaller than any of the statements it replaces.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class BadgeStateQuery(IApplicationReadDbConnection dbConnection) : IBadgeStateQuery
{
    public async Task<BadgeStateData> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        var awards = await GetAwardsAsync(userId, cancellationToken);
        var rounds = await GetRoundsAsync(userId, cancellationToken);
        var owner = await GetOwnerAsync(userId, cancellationToken);

        return new BadgeStateData(owner?.FirstName, owner?.LastName, awards, rounds, owner?.LeaguesJoined ?? 0);
    }

    /// <summary>Every award, ungrouped: the two screens count the same rows differently.</summary>
    private async Task<IReadOnlyList<BadgeAwardRow>> GetAwardsAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ub.[BadgeKey],
                ub.[AwardedUtc]
            FROM
                [UserBadges] ub
            WHERE
                ub.[UserId] = @UserId;";

        return (await dbConnection.QueryAsync<BadgeAwardRow>(sql, cancellationToken, new { UserId = userId })).ToList();
    }

    private async Task<IReadOnlyList<BadgeRoundRow>> GetRoundsAsync(string userId, CancellationToken cancellationToken)
    {
        // [UserExactScoreCount] comes back null when this player has no result for the round, which is what tells a
        // round they sat out apart from one they scored nothing in. MAX rather than a bare column so the shape cannot
        // depend on the uniqueness of a row per player per round.
        const string sql = @"
            SELECT
                r.[SeasonId],
                r.[RoundNumber],
                r.[Status],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [RoundResults] rr
                    WHERE
                        rr.[RoundId] = r.[Id]
                ) THEN 1 ELSE 0 END AS bit) AS [HasAnyResult],
                (
                    SELECT
                        MAX(rr.[ExactScoreCount])
                    FROM
                        [RoundResults] rr
                    WHERE
                        rr.[RoundId] = r.[Id]
                        AND rr.[UserId] = @UserId
                ) AS [UserExactScoreCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Matches] m
                    WHERE
                        m.[RoundId] = r.[Id]
                ) AS [MatchCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [UserPredictions] up
                    INNER JOIN
                        [Matches] m ON m.[Id] = up.[MatchId]
                    WHERE
                        m.[RoundId] = r.[Id]
                        AND up.[UserId] = @UserId
                ) AS [UserPredictionCount]
            FROM
                [Rounds] r;";

        return (await dbConnection.QueryAsync<BadgeRoundRow>(sql, cancellationToken, new { UserId = userId })).ToList();
    }

    private async Task<OwnerRow?> GetOwnerAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[FirstName],
                u.[LastName],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[UserId] = u.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [LeaguesJoined]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Id] = @UserId;";

        return await dbConnection.QuerySingleOrDefaultAsync<OwnerRow>(
            sql, cancellationToken,
            new { UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
    }

    /// <summary>The player themselves, and the one metric that is not about rounds.</summary>
    private sealed record OwnerRow(string? FirstName, string? LastName, int LeaguesJoined);
}

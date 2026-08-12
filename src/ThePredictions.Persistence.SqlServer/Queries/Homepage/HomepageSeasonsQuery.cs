using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Homepage.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Homepage;

/// <summary>
/// The SQL Server reads behind <see cref="IHomepageSeasonsQuery"/>.
///
/// Three reads where there was one statement with two derived tables. What is gone: three <c>GETUTCDATE()</c> calls, the prize
/// pot arithmetic, the distinct player count and the ordering.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class HomepageSeasonsQuery(IApplicationReadDbConnection dbConnection) : IHomepageSeasonsQuery
{
    public async Task<HomepageSeasonsData> ExecuteAsync(CancellationToken cancellationToken)
    {
        var seasons = await GetSeasonsAsync(cancellationToken);

        if (seasons.Count == 0)
            return new HomepageSeasonsData(seasons, [], []);

        var leagues = await GetLeaguesAsync(cancellationToken);
        var memberships = await GetMembershipsAsync(cancellationToken);

        return new HomepageSeasonsData(seasons, leagues, memberships);
    }

    /// <remarks>
    /// Every season, including ones that have finished. Which of them the homepage still advertises is measured against the
    /// injected clock, so the read cannot decide it.
    /// </remarks>
    private async Task<IReadOnlyList<HomepageSeasonRow>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                c.[Type] AS [CompetitionType],
                s.[StartDateUtc],
                s.[EndDateUtc]
            FROM
                [Seasons] s
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId];";

        return (await dbConnection.QueryAsync<HomepageSeasonRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<HomepageLeagueRow>> GetLeaguesAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[SeasonId],
                l.[Id] AS [LeagueId],
                l.[Price],
                l.[PrizeFundOverride],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [ApprovedMemberCount]
            FROM
                [Leagues] l;";

        return (await dbConnection.QueryAsync<HomepageLeagueRow>(
            sql, cancellationToken, new { ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    /// <remarks>
    /// One row per approved membership rather than a count, because a player in three of a season's leagues is one player and
    /// collapsing that is a rule.
    /// </remarks>
    private async Task<IReadOnlyList<HomepageMembershipRow>> GetMembershipsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[SeasonId],
                lm.[UserId]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]
            WHERE
                lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<HomepageMembershipRow>(
            sql, cancellationToken, new { ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}

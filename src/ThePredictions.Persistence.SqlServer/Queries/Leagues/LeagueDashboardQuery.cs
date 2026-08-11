using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueDashboardQuery"/>.
///
/// Two reads, neither of which filters members by status, orders anything, names anybody or works out a pot. The
/// rounds come from <c>ILeagueRoundsQuery</c>, shared with the dashboard's round picker, and the membership check the
/// handler used to make for itself belongs to <c>ILeagueMembershipQuery</c>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueDashboardQuery(IApplicationReadDbConnection dbConnection) : ILeagueDashboardQuery
{
    public async Task<LeagueDashboardData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var header = await GetHeaderAsync(leagueId, cancellationToken);

        if (header == null)
            return null;

        var members = await GetMembersAsync(leagueId, cancellationToken);

        return new LeagueDashboardData(header, members);
    }

    private async Task<LeagueDashboardHeaderRow?> GetHeaderAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Name],
                c.[Type] AS [CompetitionType],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                l.[EntryDeadlineUtc],
                l.[Price],
                l.[PrizeFundOverride],
                l.[IsFree],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [MemberCount],
                s.[NumberOfRounds],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Rounds] r
                    WHERE
                        r.[SeasonId] = s.[Id]
                        AND r.[Status] = @CompletedStatus
                ) AS [CompletedRoundCount]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                l.[Id] = @LeagueId;";

        return await dbConnection.QuerySingleOrDefaultAsync<LeagueDashboardHeaderRow>(
            sql, cancellationToken,
            new
            {
                LeagueId = leagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                CompletedStatus = nameof(RoundStatus.Completed)
            });
    }

    private async Task<IReadOnlyList<LeagueDashboardMemberRow>> GetMembersAsync(int leagueId, CancellationToken cancellationToken)
    {
        // Every membership, whatever its status. Which of them the dashboard lists is a rule - approved members and
        // pending requests, but not people who were turned away.
        const string sql = @"
            SELECT
                u.[FirstName],
                u.[LastName],
                lm.[Status],
                lm.[JoinedAtUtc]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            WHERE
                lm.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<LeagueDashboardMemberRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }
}

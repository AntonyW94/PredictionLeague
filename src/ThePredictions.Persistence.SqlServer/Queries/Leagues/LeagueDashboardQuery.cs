using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueDashboardQuery"/>.
///
/// Three reads, none of which filters members by status, orders anything, names anybody or works out a pot. The
/// membership check the handler used to make for itself has gone entirely - it belongs to
/// <c>ILeagueMembershipQuery</c>, which every other league query already used.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueDashboardQuery(IApplicationReadDbConnection dbConnection) : ILeagueDashboardQuery
{
    public async Task<LeagueDashboardData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var header = await GetHeaderAsync(leagueId, cancellationToken);

        if (header == null)
            return null;

        var roundsTask = GetRoundsAsync(leagueId, cancellationToken);
        var membersTask = GetMembersAsync(leagueId, cancellationToken);

        await Task.WhenAll(roundsTask, membersTask);

        return new LeagueDashboardData(header, roundsTask.Result, membersTask.Result);
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

    private async Task<IReadOnlyList<LeagueDashboardRoundRow>> GetRoundsAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id] AS [RoundId],
                r.[SeasonId],
                r.[RoundNumber],
                r.[ApiRoundName],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [Matches] m
                    WHERE
                        m.[RoundId] = r.[Id]
                ) AS [MatchCount]
            FROM
                [Rounds] r
            INNER JOIN
                [Leagues] l ON l.[SeasonId] = r.[SeasonId]
            WHERE
                l.[Id] = @LeagueId;";

        return (await dbConnection.QueryAsync<LeagueDashboardRoundRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
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

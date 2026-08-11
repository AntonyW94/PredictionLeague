using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeaguePrizesPageQuery"/>.
///
/// Two reads instead of one left join, so a league with four prizes no longer comes back as four copies of its own
/// details and a league with none no longer comes back as a row of nulls.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeaguePrizesPageQuery(IApplicationReadDbConnection dbConnection) : ILeaguePrizesPageQuery
{
    public async Task<LeaguePrizesPageData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string headerSql = @"
            SELECT
                l.[Name] AS [LeagueName],
                l.[EntryDeadlineUtc],
                l.[Price],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                ) AS [TotalMembershipCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [ApprovedMemberCount],
                s.[NumberOfRounds],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                s.[EndDateUtc] AS [SeasonEndDateUtc]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            WHERE
                l.[Id] = @LeagueId;";

        var header = await dbConnection.QuerySingleOrDefaultAsync<LeaguePrizesHeaderRow>(
            headerSql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        if (header == null)
            return null;

        const string prizesSql = @"
            SELECT
                ps.[PrizeType],
                ps.[Rank],
                ps.[PrizeAmount],
                ps.[Stage]
            FROM
                [LeaguePrizeSettings] ps
            WHERE
                ps.[LeagueId] = @LeagueId;";

        var prizes = (await dbConnection.QueryAsync<LeaguePrizeSettingRow>(
            prizesSql, cancellationToken, new { LeagueId = leagueId })).ToList();

        return new LeaguePrizesPageData(header, prizes);
    }
}

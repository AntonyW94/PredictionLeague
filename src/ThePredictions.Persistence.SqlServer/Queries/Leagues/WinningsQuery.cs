using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="IWinningsQuery"/>.
///
/// Four reads, the same four the handler used to make for itself. Nothing here names a player, formats a month, decides
/// whether prizes have been worked out yet, or works out a pot.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class WinningsQuery(IApplicationReadDbConnection dbConnection) : IWinningsQuery
{
    public async Task<WinningsData?> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var header = await dbConnection.QuerySingleOrDefaultAsync<WinningsHeaderRow>(
            @"
            SELECT
                l.[EntryDeadlineUtc],
                l.[Price] AS [EntryCost],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [EntryCount],
                l.[PrizeFundOverride],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                s.[EndDateUtc] AS [SeasonEndDateUtc],
                s.[NumberOfRounds] AS [TotalRoundsInSeason]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            WHERE
                l.[Id] = @LeagueId;",
            cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        if (header == null)
            return null;

        var settingsTask = GetPrizeSettingsAsync(leagueId, cancellationToken);
        var winningsTask = GetWinningsAsync(leagueId, cancellationToken);
        var membersTask = GetMembersAsync(leagueId, cancellationToken);

        await Task.WhenAll(settingsTask, winningsTask, membersTask);

        return new WinningsData(header, settingsTask.Result, winningsTask.Result, membersTask.Result);
    }

    private async Task<IReadOnlyList<WinningsPrizeSettingRow>> GetPrizeSettingsAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ps.[Id],
                ps.[PrizeType],
                ps.[PrizeDescription] AS [Name],
                ps.[PrizeAmount] AS [Amount],
                ps.[Stage]
            FROM
                [LeaguePrizeSettings] ps
            WHERE
                ps.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<WinningsPrizeSettingRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<WinningsRow>> GetWinningsAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                w.[Amount],
                w.[LeaguePrizeSettingId],
                lps.[PrizeType],
                u.[FirstName],
                u.[LastName],
                w.[RoundNumber],
                w.[Month],
                w.[UserId]
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = w.[UserId]
            WHERE
                lps.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<WinningsRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<LeaderboardParticipantRow>> GetMembersAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lm.[UserId],
                u.[FirstName],
                u.[LastName]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<LeaderboardParticipantRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}

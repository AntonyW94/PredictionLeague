using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Rounds.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Rounds;

/// <summary>
/// The SQL Server reads behind <see cref="IPrizeWinnersQuery"/>.
///
/// Three reads where there was one statement over five tables and two left joins. What is gone: the
/// <c>ISNULL(..., -1)</c> sentinel that matched a winning against the sent-log, the <c>w.[Amount] &gt; 0</c> filter, the
/// round-name lookups and the ordering.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class PrizeWinnersQuery(IApplicationReadDbConnection dbConnection) : IPrizeWinnersQuery
{
    public async Task<PrizeWinnersData> ExecuteAsync(int roundId, CancellationToken cancellationToken)
    {
        var winnings = await GetWinningsAsync(roundId, cancellationToken);

        if (winnings.Count == 0)
            return new PrizeWinnersData(winnings, [], []);

        var notifications = await GetNotificationsAsync(roundId, cancellationToken);
        var seasonRounds = await GetSeasonRoundNamesAsync(roundId, cancellationToken);

        return new PrizeWinnersData(winnings, notifications, seasonRounds);
    }

    /// <remarks>
    /// Every winning across the round's season, including ones worth nothing. Whether a prize of zero is worth an email is a
    /// rule.
    /// </remarks>
    private async Task<IReadOnlyList<PrizeWinningRow>> GetWinningsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[Email],
                u.[FirstName],
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                lps.[Id] AS [LeaguePrizeSettingId],
                lps.[PrizeType],
                lps.[PrizeDescription],
                lps.[Rank],
                lps.[Stage],
                w.[Amount],
                w.[RoundNumber],
                w.[Month]
            FROM
                [Rounds] r
            INNER JOIN
                [Leagues] l ON l.[SeasonId] = r.[SeasonId]
            INNER JOIN
                [LeaguePrizeSettings] lps ON lps.[LeagueId] = l.[Id]
            INNER JOIN
                [Winnings] w ON w.[LeaguePrizeSettingId] = lps.[Id]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = w.[UserId]
            WHERE
                r.[Id] = @RoundId;";

        return (await dbConnection.QueryAsync<PrizeWinningRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }

    /// <remarks>
    /// Every notification sent for the season's prize slots. Matching one to a winning is a rule, so nothing is joined here.
    /// </remarks>
    private async Task<IReadOnlyList<PrizeNotificationRow>> GetNotificationsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                pn.[UserId],
                pn.[LeaguePrizeSettingId],
                pn.[RoundNumber],
                pn.[Month]
            FROM
                [PrizeNotifications] pn
            INNER JOIN
                [LeaguePrizeSettings] lps ON lps.[Id] = pn.[LeaguePrizeSettingId]
            INNER JOIN
                [Leagues] l ON l.[Id] = lps.[LeagueId]
            INNER JOIN
                [Rounds] r ON r.[SeasonId] = l.[SeasonId]
            WHERE
                r.[Id] = @RoundId;";

        return (await dbConnection.QueryAsync<PrizeNotificationRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }

    private async Task<IReadOnlyList<SeasonRoundNameRow>> GetSeasonRoundNamesAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sr.[RoundNumber],
                sr.[DisplayName]
            FROM
                [Rounds] sr
            WHERE
                sr.[SeasonId] = (
                    SELECT
                        r.[SeasonId]
                    FROM
                        [Rounds] r
                    WHERE
                        r.[Id] = @RoundId
                );";

        return (await dbConnection.QueryAsync<SeasonRoundNameRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }
}

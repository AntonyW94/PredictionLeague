using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.External;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueWelcomeBatchQuery"/>.
///
/// Seven reads where there were four statements, the first of which was four levels deep in nested <c>NOT EXISTS</c> blocks. What
/// is gone from it: the check against the sent-log, the exclusion of a league whose prize scheme has no settings worked out from
/// it, the enabled-only filter on the boost rules and their windows, and the ordering.
/// </summary>
/// <remarks>
/// The entry-deadline window stays. It is choosing which rows to read, and both instants are the caller's - the job passes its own
/// clock in, which is why this part was already testable.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueWelcomeBatchQuery(IApplicationReadDbConnection dbConnection) : ILeagueWelcomeBatchQuery
{
    public async Task<LeagueWelcomeBatchData> ExecuteAsync(
        DateTime windowStartUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var leagues = await GetLeaguesAsync(windowStartUtc, nowUtc, cancellationToken);

        if (leagues.Count == 0)
            return new LeagueWelcomeBatchData(leagues, [], [], [], [], [], []);

        var leagueIds = leagues.Select(league => league.LeagueId).ToArray();

        var recipients = await GetRecipientsAsync(leagueIds, cancellationToken);
        var alreadyNotified = await GetAlreadyNotifiedAsync(leagueIds, cancellationToken);
        var schemes = await GetSchemesAsync(leagueIds, cancellationToken);
        var prizes = await GetPrizesAsync(leagueIds, cancellationToken);
        var boosts = await GetBoostsAsync(leagueIds, cancellationToken);
        var boostWindows = await GetBoostWindowsAsync(leagueIds, cancellationToken);

        return new LeagueWelcomeBatchData(leagues, recipients, alreadyNotified, schemes, prizes, boosts, boostWindows);
    }

    /// <remarks>
    /// The window is inclusive at both ends: a league whose deadline falls exactly on either boundary belongs to this run, which
    /// is what stops one slipping between two runs of the job.
    /// </remarks>
    private async Task<IReadOnlyList<WelcomeLeagueRow>> GetLeaguesAsync(
        DateTime windowStartUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                s.[Name] AS [SeasonName],
                l.[HasPrizes],
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
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                s.[EndDateUtc] AS [SeasonEndDateUtc]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            WHERE
                l.[EntryDeadlineUtc] <= @NowUtc
                AND l.[EntryDeadlineUtc] >= @WindowStartUtc;";

        return (await dbConnection.QueryAsync<WelcomeLeagueRow>(
            sql, cancellationToken,
            new
            {
                NowUtc = nowUtc,
                WindowStartUtc = windowStartUtc,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            })).ToList();
    }

    private async Task<IReadOnlyList<WelcomeRecipientRow>> GetRecipientsAsync(
        int[] leagueIds,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lm.[LeagueId],
                u.[Id] AS [UserId],
                u.[Email],
                u.[FirstName]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            WHERE
                lm.[LeagueId] IN @LeagueIds
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<WelcomeRecipientRow>(
            sql, cancellationToken,
            new { LeagueIds = leagueIds, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<WelcomeNotificationRow>> GetAlreadyNotifiedAsync(
        int[] leagueIds,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lwn.[LeagueId],
                lwn.[UserId]
            FROM
                [LeagueWelcomeNotifications] lwn
            WHERE
                lwn.[LeagueId] IN @LeagueIds;";

        return (await dbConnection.QueryAsync<WelcomeNotificationRow>(
            sql, cancellationToken, new { LeagueIds = leagueIds })).ToList();
    }

    private async Task<IReadOnlyList<WelcomeSchemeRow>> GetSchemesAsync(int[] leagueIds, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lps.[LeagueId]
            FROM
                [LeaguePrizeScheme] lps
            WHERE
                lps.[LeagueId] IN @LeagueIds;";

        return (await dbConnection.QueryAsync<WelcomeSchemeRow>(sql, cancellationToken, new { LeagueIds = leagueIds })).ToList();
    }

    private async Task<IReadOnlyList<WelcomePrizeRow>> GetPrizesAsync(int[] leagueIds, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lps.[LeagueId],
                lps.[PrizeType],
                lps.[Rank],
                lps.[Stage],
                lps.[PrizeAmount] AS [Amount]
            FROM
                [LeaguePrizeSettings] lps
            WHERE
                lps.[LeagueId] IN @LeagueIds;";

        return (await dbConnection.QueryAsync<WelcomePrizeRow>(sql, cancellationToken, new { LeagueIds = leagueIds })).ToList();
    }

    /// <remarks>
    /// Switched-off rules come back too. Whether a boost is worth telling somebody about is a rule, and it also decides which
    /// windows are worth showing - which the statements this replaces had to say twice.
    /// </remarks>
    private async Task<IReadOnlyList<WelcomeBoostRow>> GetBoostsAsync(int[] leagueIds, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lbr.[Id] AS [RuleId],
                lbr.[LeagueId],
                bd.[Name],
                bd.[Description],
                bd.[ImageUrl],
                lbr.[TotalUsesPerSeason],
                lbr.[IsEnabled]
            FROM
                [LeagueBoostRules] lbr
            INNER JOIN
                [BoostDefinitions] bd ON bd.[Id] = lbr.[BoostDefinitionId]
            WHERE
                lbr.[LeagueId] IN @LeagueIds;";

        return (await dbConnection.QueryAsync<WelcomeBoostRow>(sql, cancellationToken, new { LeagueIds = leagueIds })).ToList();
    }

    private async Task<IReadOnlyList<WelcomeBoostWindowRow>> GetBoostWindowsAsync(
        int[] leagueIds,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lbw.[LeagueBoostRuleId],
                lbw.[StartRoundNumber],
                lbw.[EndRoundNumber],
                lbw.[MaxUsesInWindow]
            FROM
                [LeagueBoostWindows] lbw
            INNER JOIN
                [LeagueBoostRules] lbr ON lbr.[Id] = lbw.[LeagueBoostRuleId]
            WHERE
                lbr.[LeagueId] IN @LeagueIds;";

        return (await dbConnection.QueryAsync<WelcomeBoostWindowRow>(
            sql, cancellationToken, new { LeagueIds = leagueIds })).ToList();
    }
}

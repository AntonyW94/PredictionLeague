using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Users;

/// <summary>
/// The SQL Server reads behind <see cref="IAdminUsersQuery"/>.
///
/// A read per set, where there was one statement with eleven correlated subqueries, a <c>STRING_AGG</c>, a
/// <c>GROUP BY</c> over seven columns and an <c>ORDER BY</c> on a computed alias. What is gone: every sum and count that
/// had a definition in its <c>WHERE</c> clause, the comma-joined provider list the handler split back apart, the name
/// composition and the ordering.
/// </summary>
/// <remarks>
/// Nothing here is filtered by account. Around 45 accounts and a few hundred rows in each set, and the screen wants every
/// account anyway - so each set is one unfiltered read and the handler splits it by id, rather than one read per account.
///
/// Two things deliberately never leave the database. The password hash: only whether one exists. And the three encrypted
/// payout columns: only whether a row exists. Both were already true of the statement this replaces and are worth keeping
/// true - an administrator looking at a list of accounts has no business holding either.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class AdminUsersQuery(IApplicationReadDbConnection dbConnection) : IAdminUsersQuery
{
    public async Task<AdminUsersData> ExecuteAsync(CancellationToken cancellationToken)
    {
        var users = await GetUsersAsync(cancellationToken);

        if (users.Count == 0)
            return new AdminUsersData(users, [], [], [], [], [], [], [], []);

        var loginProviders = await GetLoginProvidersAsync(cancellationToken);
        var leagues = await GetLeaguesAsync(cancellationToken);
        var seasonPasses = await GetSeasonPassesAsync(cancellationToken);
        var winnings = await GetWinningsAsync(cancellationToken);
        var seasons = await GetSeasonsAsync(cancellationToken);
        var payoutDetailUserIds = await GetUserIdsWithPayoutDetailsAsync(cancellationToken);
        var onboardingSkips = await GetOnboardingSkipsAsync(cancellationToken);
        var badges = await GetBadgesAsync(cancellationToken);

        return new AdminUsersData(
            users, loginProviders, leagues, seasonPasses, winnings, seasons, payoutDetailUserIds, onboardingSkips, badges);
    }

    private async Task<IReadOnlyList<AdminUserRow>> GetUsersAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id],
                u.[FirstName],
                u.[LastName],
                u.[Email],
                u.[PhoneNumber],
                u.[EmailConfirmed],
                CAST(CASE WHEN u.[PasswordHash] IS NOT NULL THEN 1 ELSE 0 END AS bit) AS [HasPassword],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [AspNetUserRoles] ur
                    INNER JOIN
                        [AspNetRoles] r ON r.[Id] = ur.[RoleId]
                    WHERE
                        ur.[UserId] = u.[Id]
                        AND r.[Name] = @AdminRoleName
                ) THEN 1 ELSE 0 END AS bit) AS [IsAdmin],
                u.[TermsAcceptedAtUtc],
                u.[MarketingOptInAtUtc]
            FROM
                [AspNetUsers] u;";

        return (await dbConnection.QueryAsync<AdminUserRow>(
            sql, cancellationToken,
            new { AdminRoleName = nameof(ApplicationUserRole.Administrator) })).ToList();
    }

    private async Task<IReadOnlyList<UserLoginProviderRow>> GetLoginProvidersAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ul.[UserId],
                ul.[LoginProvider]
            FROM
                [AspNetUserLogins] ul;";

        return (await dbConnection.QueryAsync<UserLoginProviderRow>(sql, cancellationToken)).ToList();
    }

    /// <summary>
    /// One row per league an account administers or belongs to. Two selects unioned, because either side can exist without
    /// the other: an administrator need not be a member of their own league, and a member need not administer it.
    /// </summary>
    /// <remarks>
    /// The approved-member count is a correlated subquery, which the rest of this class avoids - but it is per league, not
    /// per account, and the alternative is returning every membership of every league so the handler can count them. It is
    /// only meaningful on the administrator row; a member has no use for it and gets the same figure regardless.
    /// </remarks>
    private async Task<IReadOnlyList<UserLeagueRow>> GetLeaguesAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lm.[UserId],
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                l.[SeasonId],
                CAST(0 AS bit) AS [IsAdministrator],
                lm.[Status],
                l.[IsFree],
                l.[Price],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] m
                    WHERE
                        m.[LeagueId] = l.[Id]
                        AND m.[Status] = @ApprovedStatus
                ) AS [ApprovedMemberCount]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]

            UNION ALL

            SELECT
                l.[AdministratorUserId] AS [UserId],
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                l.[SeasonId],
                CAST(1 AS bit) AS [IsAdministrator],
                NULL AS [Status],
                l.[IsFree],
                l.[Price],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] m
                    WHERE
                        m.[LeagueId] = l.[Id]
                        AND m.[Status] = @ApprovedStatus
                ) AS [ApprovedMemberCount]
            FROM
                [Leagues] l;";

        return (await dbConnection.QueryAsync<UserLeagueRow>(
            sql, cancellationToken,
            new { ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<UserSeasonPassRow>> GetSeasonPassesAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sp.[UserId],
                sp.[SeasonId],
                sp.[Tier],
                sp.[Source],
                sp.[AmountPaid],
                sp.[SmsFeePaid],
                sp.[CreatedAtUtc]
            FROM
                [SeasonPasses] sp;";

        return (await dbConnection.QueryAsync<UserSeasonPassRow>(sql, cancellationToken)).ToList();
    }

    /// <summary>
    /// One row per prize won, with the league and season it was won in and the three columns that scope it.
    /// </summary>
    /// <remarks>
    /// The prize setting is what carries the type and the tournament stage; the winning itself carries the round number and
    /// the month. Both are needed to name the prize, so both are joined.
    /// </remarks>
    private async Task<IReadOnlyList<UserWinningRow>> GetWinningsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                w.[UserId],
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                l.[SeasonId],
                ps.[PrizeType],
                ps.[Stage],
                w.[RoundNumber],
                w.[Month],
                w.[Amount],
                w.[AwardedDateUtc]
            FROM
                [Winnings] w
            INNER JOIN
                [LeaguePrizeSettings] ps ON ps.[Id] = w.[LeaguePrizeSettingId]
            INNER JOIN
                [Leagues] l ON l.[Id] = ps.[LeagueId];";

        return (await dbConnection.QueryAsync<UserWinningRow>(sql, cancellationToken)).ToList();
    }

    /// <summary>
    /// Every season, with how many rounds it holds and how many of those are complete.
    /// </summary>
    /// <remarks>
    /// Not <c>NumberOfRounds</c>, which is a number an administrator typed and which the football API can exceed. Whether a
    /// season has finished is settled from the rounds that exist, which is the definition the whole application shares.
    /// </remarks>
    private async Task<IReadOnlyList<UserSeasonRow>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id] AS [SeasonId],
                s.[Name],
                COUNT(r.[Id]) AS [RoundCount],
                COUNT(CASE WHEN r.[Status] = @CompletedStatus THEN 1 END) AS [CompletedRoundCount]
            FROM
                [Seasons] s
            LEFT JOIN
                [Rounds] r ON r.[SeasonId] = s.[Id]
            GROUP BY
                s.[Id],
                s.[Name];";

        return (await dbConnection.QueryAsync<UserSeasonRow>(
            sql, cancellationToken,
            new { CompletedStatus = nameof(RoundStatus.Completed) })).ToList();
    }

    /// <summary>
    /// The accounts that have saved payout details.
    /// </summary>
    /// <remarks>
    /// Ids only. The three account columns are encrypted at rest and this screen is not one of the two places allowed to
    /// decrypt them, so they are not selected at all - the question is whether a row exists, which is what the dashboard
    /// checklist asks too.
    /// </remarks>
    private async Task<IReadOnlyList<string>> GetUserIdsWithPayoutDetailsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                upd.[UserId]
            FROM
                [UserPayoutDetails] upd;";

        return (await dbConnection.QueryAsync<string>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<UserOnboardingSkipRow>> GetOnboardingSkipsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                uos.[UserId],
                uos.[StepKey]
            FROM
                [UserOnboardingSkips] uos;";

        return (await dbConnection.QueryAsync<UserOnboardingSkipRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<UserBadgeRow>> GetBadgesAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ub.[UserId],
                ub.[BadgeKey],
                ub.[Detail],
                ub.[AwardedUtc],
                ub.[SeasonId]
            FROM
                [UserBadges] ub;";

        return (await dbConnection.QueryAsync<UserBadgeRow>(sql, cancellationToken)).ToList();
    }
}

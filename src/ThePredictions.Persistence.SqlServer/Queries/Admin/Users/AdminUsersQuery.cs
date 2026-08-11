using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Users;

/// <summary>
/// The SQL Server reads behind <see cref="IAdminUsersQuery"/>.
///
/// Five reads where there was one statement with eleven correlated subqueries, a <c>STRING_AGG</c>, a <c>GROUP BY</c> over
/// seven columns and an <c>ORDER BY</c> on a computed alias. What is gone: every sum and count that had a definition in
/// its <c>WHERE</c> clause, the comma-joined provider list the handler split back apart, the name composition and the
/// ordering.
/// </summary>
/// <remarks>
/// The password hash never leaves the database - only whether one exists. That was already true of the statement this
/// replaces and is worth keeping true.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class AdminUsersQuery(IApplicationReadDbConnection dbConnection) : IAdminUsersQuery
{
    public async Task<AdminUsersData> ExecuteAsync(CancellationToken cancellationToken)
    {
        var users = await GetUsersAsync(cancellationToken);

        if (users.Count == 0)
            return new AdminUsersData(users, [], [], [], []);

        var loginProviders = await GetLoginProvidersAsync(cancellationToken);
        var leagues = await GetLeaguesAsync(cancellationToken);
        var seasonPasses = await GetSeasonPassesAsync(cancellationToken);
        var winnings = await GetWinningsAsync(cancellationToken);

        return new AdminUsersData(users, loginProviders, leagues, seasonPasses, winnings);
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
                ) THEN 1 ELSE 0 END AS bit) AS [IsAdmin]
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
    /// One row per league an account administers or belongs to. A full outer join, because either side can exist without
    /// the other: an administrator need not be a member of their own league, and a member need not administer it.
    /// </summary>
    private async Task<IReadOnlyList<UserLeagueRow>> GetLeaguesAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lm.[UserId],
                l.[Id] AS [LeagueId],
                CAST(0 AS bit) AS [IsAdministrator],
                lm.[Status],
                l.[IsFree],
                l.[Price]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]

            UNION ALL

            SELECT
                l.[AdministratorUserId] AS [UserId],
                l.[Id] AS [LeagueId],
                CAST(1 AS bit) AS [IsAdministrator],
                NULL AS [Status],
                l.[IsFree],
                l.[Price]
            FROM
                [Leagues] l;";

        return (await dbConnection.QueryAsync<UserLeagueRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<UserSeasonPassRow>> GetSeasonPassesAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sp.[UserId],
                sp.[Source],
                sp.[AmountPaid],
                sp.[SmsFeePaid]
            FROM
                [SeasonPasses] sp;";

        return (await dbConnection.QueryAsync<UserSeasonPassRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<UserWinningRow>> GetWinningsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                w.[UserId],
                w.[Amount]
            FROM
                [Winnings] w;";

        return (await dbConnection.QueryAsync<UserWinningRow>(sql, cancellationToken)).ToList();
    }
}

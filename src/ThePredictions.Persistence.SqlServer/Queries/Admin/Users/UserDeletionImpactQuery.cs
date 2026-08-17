using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Users;

/// <summary>
/// The SQL Server read behind <see cref="IUserDeletionImpactQuery"/>.
/// </summary>
/// <remarks>
/// One statement of scalar subqueries rather than seventeen round trips, because the whole result is a
/// single row of numbers and the dialog cannot draw itself until every one of them has arrived.
///
/// Two shapes are deliberate. The notification counts are added together into
/// <c>EmailRecords</c>: an administrator deciding whether to delete an account cares that some emails were
/// logged against it, not that three separate tables logged them. And <c>SeasonPassSpend</c> counts
/// purchased passes only, matching <c>GetAllUsersQueryHandler</c> - a trial or a comped pass is still a
/// pass, but nobody spent money on it, so including it would overstate what is being destroyed.
///
/// <b>The column order below is load-bearing.</b> It matches <see cref="UserDeletionImpactRow"/>'s
/// constructor position for position, which is how Dapper fills it. Every count is CAST to the type the
/// record declares - <c>COUNT()</c> is <c>int</c> and the sums are <c>decimal</c>, but an unaliased
/// <c>COALESCE</c> over an empty set is exactly where that quietly stops being true.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class UserDeletionImpactQuery(IApplicationReadDbConnection dbConnection) : IUserDeletionImpactQuery
{
    public async Task<UserDeletionImpactRow> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM [SeasonPasses] sp WHERE sp.[UserId] = @UserId) AS [SeasonPasses],
                (
                    SELECT
                        COALESCE(SUM(sp.[AmountPaid] + sp.[SmsFeePaid]), CAST(0 AS decimal(10, 2)))
                    FROM
                        [SeasonPasses] sp
                    WHERE
                        sp.[UserId] = @UserId
                        AND sp.[Source] = @PurchasedSource
                ) AS [SeasonPassSpend],
                (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[UserId] = @UserId) AS [LeagueMemberships],
                (SELECT COUNT(*) FROM [UserPredictions] up WHERE up.[UserId] = @UserId) AS [Predictions],
                (SELECT COUNT(*) FROM [Winnings] w WHERE w.[UserId] = @UserId) AS [Winnings],
                (
                    SELECT
                        COALESCE(SUM(w.[Amount]), CAST(0 AS decimal(18, 2)))
                    FROM
                        [Winnings] w
                    WHERE
                        w.[UserId] = @UserId
                ) AS [WinningsTotal],
                (SELECT COUNT(*) FROM [LeaguePayouts] lp WHERE lp.[UserId] = @UserId) AS [Payouts],
                (
                    SELECT
                        COALESCE(SUM(lp.[TotalAmount]), CAST(0 AS decimal(18, 2)))
                    FROM
                        [LeaguePayouts] lp
                    WHERE
                        lp.[UserId] = @UserId
                ) AS [PayoutsTotal],
                (SELECT COUNT(*) FROM [UserBadges] ub WHERE ub.[UserId] = @UserId) AS [Badges],
                (SELECT COUNT(*) FROM [UserBoostUsages] ubu WHERE ubu.[UserId] = @UserId) AS [BoostUsages],
                (SELECT COUNT(*) FROM [RoundResults] rr WHERE rr.[UserId] = @UserId) AS [RoundResults],
                (SELECT COUNT(*) FROM [LeagueRoundResults] lrr WHERE lrr.[UserId] = @UserId) AS [LeagueRoundResults],
                (SELECT COUNT(*) FROM [LeagueMemberStats] lms WHERE lms.[UserId] = @UserId) AS [LeagueStandings],
                (
                    (SELECT COUNT(*) FROM [LeagueWelcomeNotifications] lwn WHERE lwn.[UserId] = @UserId)
                    + (SELECT COUNT(*) FROM [PrizeNotifications] pn WHERE pn.[UserId] = @UserId)
                    + (SELECT COUNT(*) FROM [PredictionReminderNotifications] prn WHERE prn.[UserId] = @UserId)
                ) AS [EmailRecords],
                (SELECT COUNT(*) FROM [UserOnboardingSkips] uos WHERE uos.[UserId] = @UserId) AS [OnboardingSkips],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [UserPayoutDetails] upd
                    WHERE
                        upd.[UserId] = @UserId
                ) THEN 1 ELSE 0 END AS bit) AS [HasPayoutDetails],
                (SELECT COUNT(*) FROM [Leagues] l WHERE l.[AdministratorUserId] = @UserId) AS [LeaguesAdministered];";

        var impact = await dbConnection.QuerySingleOrDefaultAsync<UserDeletionImpactRow>(
            sql, cancellationToken,
            new { UserId = userId, PurchasedSource = nameof(SeasonPassSource.Purchased) });

        // A SELECT of scalar subqueries has no FROM, so it returns exactly one row for any user id -
        // including one that does not exist, which yields a row of zeroes. Null here would mean the
        // statement did not run as written, which is a server-side defect rather than a missing account.
        return impact
               ?? throw new InvalidOperationException("The user deletion impact read returned no row, which a SELECT with no FROM cannot do.");
    }
}

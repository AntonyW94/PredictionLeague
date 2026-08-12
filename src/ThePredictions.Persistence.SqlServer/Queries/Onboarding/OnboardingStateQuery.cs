using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Onboarding.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Onboarding;

/// <summary>
/// The SQL Server reads behind <see cref="IOnboardingStateQuery"/>. The <c>LEN(LTRIM(RTRIM(...))) &gt; 0</c> test on the phone
/// number is gone; the number comes back as stored.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class OnboardingStateQuery(IApplicationReadDbConnection dbConnection) : IOnboardingStateQuery
{
    public async Task<OnboardingStateRow> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [SeasonPasses] sp
                    WHERE
                        sp.[UserId] = @UserId
                ) AS [PassCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[UserId] = @UserId
                ) AS [LeagueCount],
                (
                    SELECT
                        u.[PhoneNumber]
                    FROM
                        [AspNetUsers] u
                    WHERE
                        u.[Id] = @UserId
                ) AS [PhoneNumber],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [UserPayoutDetails] upd
                    WHERE
                        upd.[UserId] = @UserId
                ) THEN 1 ELSE 0 END AS bit) AS [HasPayoutDetails];";

        return await dbConnection.QuerySingleOrDefaultAsync<OnboardingStateRow>(sql, cancellationToken, new { UserId = userId })
               ?? new OnboardingStateRow(0, 0, null, false);
    }

    public async Task<IReadOnlyList<string>> GetSkippedStepKeysAsync(string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                uos.[StepKey]
            FROM
                [UserOnboardingSkips] uos
            WHERE
                uos.[UserId] = @UserId;";

        return (await dbConnection.QueryAsync<string>(sql, cancellationToken, new { UserId = userId })).ToList();
    }
}

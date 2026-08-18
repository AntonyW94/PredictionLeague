using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Dashboard;

/// <summary>
/// The SQL Server reads behind <see cref="IActiveRoundsQuery"/>.
///
/// Two reads. What is gone from them: the <c>COALESCE</c> over a correlated <c>MAX</c> that worked out when a round's last
/// match locks, the ordering that put in-progress rounds first, and the two <c>ORDER BY</c> clauses on the matches. All three
/// were rules.
/// </summary>
/// <remarks>
/// What remains is scoping and counting. The three prediction-split counts stay here because they are counts over every player
/// who predicted a match, and the classification they encode - a scoreline leaning home, level, or away - is pinned by the
/// conformance tests rather than moved, since moving it would mean reading every prediction in the round.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class ActiveRoundsQuery(IApplicationReadDbConnection dbConnection) : IActiveRoundsQuery
{
    public async Task<ActiveRoundsData> ExecuteAsync(string userId, CancellationToken cancellationToken)
    {
        var rounds = await GetRoundsAsync(userId, cancellationToken);

        if (rounds.Count == 0)
            return new ActiveRoundsData(rounds, []);

        var matches = await GetMatchesAsync(userId, rounds.Select(round => round.RoundId).ToArray(), cancellationToken);

        return new ActiveRoundsData(rounds, matches);
    }

    private async Task<IReadOnlyList<ActiveRoundCandidateRow>> GetRoundsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        // HasConfirmedMatch looks at every match including postponed ones, which is what the old EXISTS did. The matches read
        // below excludes them, so working this out from those rows would drop a round whose only confirmed match was called off.
        const string sql = @"
            SELECT
                r.[Id] AS [RoundId],
                s.[Name] AS [SeasonName],
                r.[RoundNumber],
                r.[DisplayName] AS [RoundDisplayName],
                r.[DeadlineUtc],
                r.[Status],
                c.[Type] AS [CompetitionType],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [UserPredictions] up
                    INNER JOIN
                        [Matches] m ON m.[Id] = up.[MatchId]
                    WHERE
                        m.[RoundId] = r.[Id]
                        AND up.[UserId] = @UserId
                ) THEN 1 ELSE 0 END AS bit) AS [HasUserPredicted],
                CAST(CASE WHEN EXISTS (
                    SELECT
                        1
                    FROM
                        [Matches] m
                    WHERE
                        m.[RoundId] = r.[Id]
                        AND m.[HomeTeamId] IS NOT NULL
                        AND m.[AwayTeamId] IS NOT NULL
                ) THEN 1 ELSE 0 END AS bit) AS [HasConfirmedMatch]
            FROM
                [Rounds] r
            INNER JOIN
                [Seasons] s ON s.[Id] = r.[SeasonId]
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                r.[Status] NOT IN (@DraftStatus, @CompletedStatus)
                AND s.[IsActive] = 1
                AND r.[SeasonId] IN (
                    SELECT
                        l.[SeasonId]
                    FROM
                        [Leagues] l
                    INNER JOIN
                        [LeagueMembers] lm ON lm.[LeagueId] = l.[Id]
                    WHERE
                        lm.[UserId] = @UserId
                        AND lm.[Status] = @ApprovedStatus
                );";

        return (await dbConnection.QueryAsync<ActiveRoundCandidateRow>(
            sql, cancellationToken,
            new
            {
                UserId = userId,
                DraftStatus = nameof(RoundStatus.Draft),
                CompletedStatus = nameof(RoundStatus.Completed),
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            })).ToList();
    }

    private async Task<IReadOnlyList<ActiveRoundMatchRow>> GetMatchesAsync(
        string userId,
        int[] roundIds,
        CancellationToken cancellationToken)
    {
        // Postponed matches are left out, and that is not only cosmetic: a postponed match must not hold a round open, and the
        // round's latest prediction deadline is worked out from these rows.
        const string sql = @"
            SELECT
                m.[RoundId],
                ht.[LogoUrl] AS [HomeTeamLogoUrl],
                at.[LogoUrl] AS [AwayTeamLogoUrl],
                ht.[ShortName] AS [HomeTeamShortName],
                at.[ShortName] AS [AwayTeamShortName],
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                up.[Outcome],
                m.[Status],
                m.[ActualHomeTeamScore] AS [ActualHomeScore],
                m.[ActualAwayTeamScore] AS [ActualAwayScore],
                m.[MatchDateTimeUtc],
                m.[MatchNumber],
                CAST(CASE WHEN m.[HomeTeamId] IS NOT NULL AND m.[AwayTeamId] IS NOT NULL THEN 1 ELSE 0 END AS bit)
                    AS [AreTeamsConfirmed],
                m.[PlaceholderHomeName],
                m.[PlaceholderAwayName],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [UserPredictions] hp
                    WHERE
                        hp.[MatchId] = m.[Id]
                        AND hp.[PredictedHomeScore] > hp.[PredictedAwayScore]
                ) AS [HomeCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [UserPredictions] dp
                    WHERE
                        dp.[MatchId] = m.[Id]
                        AND dp.[PredictedHomeScore] = dp.[PredictedAwayScore]
                ) AS [DrawCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [UserPredictions] ap
                    WHERE
                        ap.[MatchId] = m.[Id]
                        AND ap.[PredictedHomeScore] < ap.[PredictedAwayScore]
                ) AS [AwayCount],
                m.[CustomLockTimeUtc]
            FROM
                [Matches] m
            LEFT JOIN
                [Teams] ht ON ht.[Id] = m.[HomeTeamId]
            LEFT JOIN
                [Teams] at ON at.[Id] = m.[AwayTeamId]
            LEFT JOIN
                [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = @UserId
            WHERE
                m.[RoundId] IN @RoundIds
                AND m.[Status] <> @PostponedStatus;";

        return (await dbConnection.QueryAsync<ActiveRoundMatchRow>(
            sql, cancellationToken,
            new { UserId = userId, RoundIds = roundIds, PostponedStatus = nameof(MatchStatus.Postponed) })).ToList();
    }
}

using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Rounds;

/// <summary>
/// The SQL Server reads behind <see cref="IRoundDigestQuery"/>.
///
/// Four reads where there was one statement over six tables and two CTEs. What is gone from it: the
/// <c>ROW_NUMBER() OVER (PARTITION BY LeagueId ...)</c> that picked each league's top scorer and the tie-break inside
/// it, the <c>TOP 1 ... WHERE RoundNumber &gt; cur.RoundNumber</c> that found the next round, the <c>CASE</c> that
/// subtracted two cached positions, the <c>EXISTS</c> that decided who gets an email, the name abbreviation, both
/// <c>ORDER BY</c> clauses, and a <c>LEFT JOIN ... ON 1 = 1</c> that existed to staple one row onto every other.
/// </summary>
/// <remarks>
/// Every read here is scoped to one round, so the row counts are a handful each: the season's rounds, the players
/// scored in the round, their approved memberships, and the league points for that round.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class RoundDigestQuery(IApplicationReadDbConnection dbConnection) : IRoundDigestQuery
{
    public async Task<RoundDigestData> ExecuteAsync(int roundId, CancellationToken cancellationToken)
    {
        var seasonRounds = await GetSeasonRoundsAsync(roundId, cancellationToken);

        if (seasonRounds.Count == 0)
            return new RoundDigestData(seasonRounds, [], [], []);

        var players = await GetPlayersAsync(roundId, cancellationToken);
        var memberships = await GetMembershipsAsync(roundId, cancellationToken);
        var leagueScores = await GetLeagueScoresAsync(roundId, cancellationToken);

        return new RoundDigestData(seasonRounds, players, memberships, leagueScores);
    }

    /// <summary>
    /// Every round of the season the requested round belongs to - including that round, which is how the caller finds
    /// it, and which is why nothing comes back at all for a round id that does not exist.
    /// </summary>
    private async Task<IReadOnlyList<RoundDigestRoundRow>> GetSeasonRoundsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id],
                r.[RoundNumber],
                r.[DisplayName],
                r.[DeadlineUtc]
            FROM
                [Rounds] r
            WHERE
                r.[SeasonId] = (
                    SELECT
                        cur.[SeasonId]
                    FROM
                        [Rounds] cur
                    WHERE
                        cur.[Id] = @RoundId
                );";

        return (await dbConnection.QueryAsync<RoundDigestRoundRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }

    private async Task<IReadOnlyList<RoundDigestPlayerRow>> GetPlayersAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[Email],
                u.[FirstName],
                rr.[ExactScoreCount],
                rr.[CorrectResultCount],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [UserPredictions] up
                    INNER JOIN
                        [Matches] m ON m.[Id] = up.[MatchId]
                    WHERE
                        m.[RoundId] = rr.[RoundId]
                        AND up.[UserId] = u.[Id]
                ) AS [PredictionCount]
            FROM
                [RoundResults] rr
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = rr.[UserId]
            WHERE
                rr.[RoundId] = @RoundId;";

        return (await dbConnection.QueryAsync<RoundDigestPlayerRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }

    private async Task<IReadOnlyList<RoundDigestMembershipRow>> GetMembershipsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lm.[UserId],
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                lms.[OverallRank],
                lms.[SnapshotOverallRank]
            FROM
                [Leagues] l
            INNER JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = l.[Id]
                AND lm.[Status] = @ApprovedStatus
            LEFT JOIN
                [LeagueMemberStats] lms ON lms.[LeagueId] = l.[Id]
                AND lms.[UserId] = lm.[UserId]
            WHERE
                l.[SeasonId] = (
                    SELECT
                        cur.[SeasonId]
                    FROM
                        [Rounds] cur
                    WHERE
                        cur.[Id] = @RoundId
                );";

        return (await dbConnection.QueryAsync<RoundDigestMembershipRow>(
            sql, cancellationToken,
            new { RoundId = roundId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<RoundLeagueScoreRow>> GetLeagueScoresAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lrr.[LeagueId],
                lrr.[UserId],
                u.[FirstName],
                u.[LastName],
                lrr.[BoostedPoints]
            FROM
                [LeagueRoundResults] lrr
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = lrr.[UserId]
            WHERE
                lrr.[RoundId] = @RoundId;";

        return (await dbConnection.QueryAsync<RoundLeagueScoreRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }
}

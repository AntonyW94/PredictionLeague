using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="IStageLeaderboardQuery"/>.
///
/// Scoping only. In particular the <c>CASE WHEN trm.[Stages] LIKE '%Group%'</c> has gone: classifying a round's
/// stage is a rule, and one whose old behaviour depended on the collation being case-insensitive. The raw stage
/// text comes back instead.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class StageLeaderboardQuery(IApplicationReadDbConnection dbConnection) : IStageLeaderboardQuery
{
    public async Task<StageLeaderboardData> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var membersTask = GetMembersAsync(leagueId, cancellationToken);
        var roundsTask = GetSeasonRoundsAsync(leagueId, cancellationToken);
        var pointsTask = GetRoundPointsAsync(leagueId, cancellationToken);
        var inProgressTask = HasRoundInProgressAsync(leagueId, cancellationToken);

        await Task.WhenAll(membersTask, roundsTask, pointsTask, inProgressTask);

        return new StageLeaderboardData(
            membersTask.Result, roundsTask.Result, pointsTask.Result, inProgressTask.Result);
    }

    private async Task<IReadOnlyList<LeaderboardParticipantRow>> GetMembersAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[FirstName],
                u.[LastName]
            FROM
                [LeagueMembers] lm
            INNER JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<LeaderboardParticipantRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<SeasonRoundStageRow>> GetSeasonRoundsAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        // LEFT JOIN: a round with no tournament mapping has no stage text, which the classifier treats as
        // knockout - the same arm the old CASE's ELSE fell to.
        const string sql = @"
            SELECT
                r.[Id] AS [RoundId],
                trm.[Stages],
                r.[Status]
            FROM
                [Rounds] r
            INNER JOIN
                [Leagues] l ON l.[SeasonId] = r.[SeasonId]
            LEFT JOIN
                [TournamentRoundMappings] trm
                    ON trm.[SeasonId] = r.[SeasonId] AND trm.[RoundNumber] = r.[RoundNumber]
            WHERE
                l.[Id] = @LeagueId;";

        var rows = await dbConnection.QueryAsync<SeasonRoundStageStringRow>(
            sql, cancellationToken, new { LeagueId = leagueId });

        return rows
            .Select(row => new SeasonRoundStageRow(row.RoundId, row.Stages, Enum.Parse<RoundStatus>(row.Status)))
            .ToList();
    }

    private async Task<IReadOnlyList<MemberRoundPointsByRoundRow>> GetRoundPointsAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lrr.[UserId],
                lrr.[RoundId],
                lrr.[BoostedPoints]
            FROM
                [LeagueRoundResults] lrr
            WHERE
                lrr.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<MemberRoundPointsByRoundRow>(
            sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<bool> HasRoundInProgressAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                CAST(CASE WHEN EXISTS (
                    SELECT 1
                    FROM [Rounds] r
                    INNER JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                    WHERE l.[Id] = @LeagueId AND r.[Status] = @Status
                ) THEN 1 ELSE 0 END AS bit);";

        return await dbConnection.QuerySingleOrDefaultAsync<bool>(
            sql, cancellationToken, new { LeagueId = leagueId, Status = nameof(RoundStatus.InProgress) });
    }

    // Status arrives as its stored name; parsed above so the port hands over the enum.
    private sealed record SeasonRoundStageStringRow(int RoundId, string? Stages, string Status);
}

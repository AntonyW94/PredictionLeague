using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="IExactScoresLeaderboardQuery"/>.
///
/// Scoping only. The <c>OUTER APPLY</c> that used to total each member's exact scores has gone with the total
/// itself; what is left is two narrow reads.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class ExactScoresLeaderboardQuery(IApplicationReadDbConnection dbConnection) : IExactScoresLeaderboardQuery
{
    public async Task<ExactScoresLeaderboardData> ExecuteAsync(int leagueId, CancellationToken cancellationToken)
    {
        var membersTask = GetMembersAsync(leagueId, cancellationToken);
        var exactScoresTask = GetExactScoresAsync(leagueId, cancellationToken);

        await Task.WhenAll(membersTask, exactScoresTask);

        return new ExactScoresLeaderboardData(membersTask.Result, exactScoresTask.Result);
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
                [AspNetUsers] u ON u.[Id] = lm.[UserId]
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<LeaderboardParticipantRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<MemberExactScoresRow>> GetExactScoresAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        // RoundResults is global per user per round, so this is scoped by the league's season rather than by the
        // league. Restricted to the league's approved members so a non-member's rows cannot arrive and be
        // silently ignored downstream.
        const string sql = @"
            SELECT
                rr.[UserId],
                rr.[ExactScoreCount]
            FROM
                [RoundResults] rr
            INNER JOIN
                [Rounds] r ON r.[Id] = rr.[RoundId]
            INNER JOIN
                [Leagues] l ON l.[SeasonId] = r.[SeasonId]
            INNER JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = l.[Id] AND lm.[UserId] = rr.[UserId]
            WHERE
                l.[Id] = @LeagueId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<MemberExactScoresRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}

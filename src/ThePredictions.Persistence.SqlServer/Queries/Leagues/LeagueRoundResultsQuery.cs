using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Persistence.SqlServer.Queries.Leagues;

/// <summary>
/// The SQL Server reads behind <see cref="ILeagueRoundResultsQuery"/>.
///
/// Five scoped reads in place of one statement whose <c>CROSS JOIN</c> multiplied members by fixtures. Nothing
/// here ranks, hides, defaults or sums. In particular there is no <c>GETUTCDATE()</c>: the fixture lock the grid
/// hides predictions by is compared against an injected clock in the handler, so the boundary can be pinned by a
/// test rather than raced against the database's own idea of now.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class LeagueRoundResultsQuery(IApplicationReadDbConnection dbConnection) : ILeagueRoundResultsQuery
{
    public async Task<LeagueRoundResultsData?> ExecuteAsync(int leagueId, int roundId, CancellationToken cancellationToken)
    {
        var roundRow = await dbConnection.QuerySingleOrDefaultAsync<RoundRow>(
            @"
            SELECT
                r.[Id],
                r.[SeasonId],
                r.[RoundNumber],
                r.[DisplayName],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                r.[ApiRoundName]
            FROM
                [Rounds] r
            WHERE
                r.[Id] = @RoundId;",
            cancellationToken,
            new { RoundId = roundId });

        if (roundRow == null)
            return null;

        var fixturesTask = GetFixturesAsync(roundId, cancellationToken);
        var membersTask = GetMembersAsync(leagueId, cancellationToken);
        var predictionsTask = GetPredictionsAsync(leagueId, roundId, cancellationToken);
        var pointsTask = GetPointsAsync(leagueId, roundId, cancellationToken);
        var boostsTask = GetBoostUsagesAsync(leagueId, roundId, cancellationToken);

        await Task.WhenAll(fixturesTask, membersTask, predictionsTask, pointsTask, boostsTask);

        var round = new Round(
            id: roundRow.Id,
            seasonId: roundRow.SeasonId,
            roundNumber: roundRow.RoundNumber,
            displayName: roundRow.DisplayName,
            startDateUtc: roundRow.StartDateUtc,
            deadlineUtc: roundRow.DeadlineUtc,
            status: Enum.Parse<RoundStatus>(roundRow.Status),
            apiRoundName: roundRow.ApiRoundName,
            lastReminderSentUtc: null,
            matches: fixturesTask.Result.Select(ToMatch).ToList(),
            resultsDigestSentUtc: null);

        return new LeagueRoundResultsData(
            round,
            membersTask.Result,
            predictionsTask.Result,
            pointsTask.Result,
            boostsTask.Result);
    }

    private static Match ToMatch(FixtureRow row) =>
        new(
            id: row.Id,
            roundId: row.RoundId,
            homeTeamId: row.HomeTeamId,
            awayTeamId: row.AwayTeamId,
            matchDateTimeUtc: row.MatchDateTimeUtc,
            customLockTimeUtc: row.CustomLockTimeUtc,
            status: Enum.Parse<MatchStatus>(row.Status),
            actualHomeTeamScore: null,
            actualAwayTeamScore: null,
            externalId: null,
            matchNumber: row.MatchNumber,
            placeholderHomeName: null,
            placeholderAwayName: null,
            apiRoundName: null);

    private async Task<IReadOnlyList<FixtureRow>> GetFixturesAsync(int roundId, CancellationToken cancellationToken)
    {
        // Every fixture in the round, postponed ones included. Which of them belong on the grid is
        // Match.IsPostponed, applied by the handler.
        const string sql = @"
            SELECT
                m.[Id],
                m.[RoundId],
                m.[HomeTeamId],
                m.[AwayTeamId],
                m.[MatchDateTimeUtc],
                m.[CustomLockTimeUtc],
                m.[Status],
                m.[MatchNumber]
            FROM
                [Matches] m
            WHERE
                m.[RoundId] = @RoundId;";

        return (await dbConnection.QueryAsync<FixtureRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }

    private async Task<IReadOnlyList<LeaderboardParticipantRow>> GetMembersAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lm.[UserId],
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

    private async Task<IReadOnlyList<MemberPredictionRow>> GetPredictionsAsync(
        int leagueId, int roundId, CancellationToken cancellationToken)
    {
        // The predictions that exist, scoped to the league's approved members. Nothing is hidden here - the
        // secrecy rule needs the fixture's lock time and an injected clock, and both live in the handler.
        const string sql = @"
            SELECT
                up.[UserId],
                up.[MatchId],
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                up.[Outcome]
            FROM
                [UserPredictions] up
            INNER JOIN
                [Matches] m ON m.[Id] = up.[MatchId]
            INNER JOIN
                [LeagueMembers] lm ON lm.[UserId] = up.[UserId]
            WHERE
                m.[RoundId] = @RoundId
                AND lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<MemberPredictionRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, RoundId = roundId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<MemberRoundPointsRow>> GetPointsAsync(
        int leagueId, int roundId, CancellationToken cancellationToken)
    {
        // Only the members who have a result row. A member without one scores zero, which the handler decides.
        const string sql = @"
            SELECT
                lrr.[UserId],
                lrr.[BoostedPoints]
            FROM
                [LeagueRoundResults] lrr
            WHERE
                lrr.[LeagueId] = @LeagueId
                AND lrr.[RoundId] = @RoundId;";

        return (await dbConnection.QueryAsync<MemberRoundPointsRow>(
            sql, cancellationToken, new { LeagueId = leagueId, RoundId = roundId })).ToList();
    }

    private async Task<IReadOnlyList<MemberBoostUsageRow>> GetBoostUsagesAsync(
        int leagueId, int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ubu.[UserId],
                bd.[Code],
                bd.[ImageUrl]
            FROM
                [UserBoostUsages] ubu
            INNER JOIN
                [BoostDefinitions] bd ON bd.[Id] = ubu.[BoostDefinitionId]
            WHERE
                ubu.[LeagueId] = @LeagueId
                AND ubu.[RoundId] = @RoundId;";

        return (await dbConnection.QueryAsync<MemberBoostUsageRow>(
            sql, cancellationToken, new { LeagueId = leagueId, RoundId = roundId })).ToList();
    }

    // Column order matches each SELECT above, per the Dapper result-mapping rule in CLAUDE.md.
    private sealed record RoundRow(
        int Id,
        int SeasonId,
        int RoundNumber,
        string DisplayName,
        DateTime StartDateUtc,
        DateTime DeadlineUtc,
        string Status,
        string? ApiRoundName);

    private sealed record FixtureRow(
        int Id,
        int RoundId,
        int? HomeTeamId,
        int? AwayTeamId,
        DateTime MatchDateTimeUtc,
        DateTime? CustomLockTimeUtc,
        string Status,
        int? MatchNumber);
}

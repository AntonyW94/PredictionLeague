using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Persistence.SqlServer.Queries.Rounds;

/// <summary>
/// The SQL Server reads behind <see cref="IRoundCompletionQuery"/>.
///
/// Every predicate here is scoping - which round, which league, which season. Nothing filters by whether a
/// fixture can still be predicted, and nothing counts anything: that was
/// <c>PredictableMatchPredicate</c>, and it is now <c>Match.IsOpenForPrediction</c> in the domain, applied
/// once by the handler. The <c>CASE</c> that named the round has gone the same way, to
/// <c>Round.GetDisplayNameOrDefault</c>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class RoundCompletionQuery(IApplicationReadDbConnection dbConnection) : IRoundCompletionQuery
{
    public async Task<RoundCompletionData?> ExecuteAsync(int roundId, int? leagueId, CancellationToken cancellationToken)
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
        var participantsTask = GetParticipantsAsync(roundId, leagueId, cancellationToken);
        var predictionsTask = GetPredictionsAsync(roundId, roundRow.SeasonId, leagueId, cancellationToken);

        await Task.WhenAll(fixturesTask, participantsTask, predictionsTask);

        var fixtures = fixturesTask.Result;

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
            matches: fixtures.Select(ToMatch).ToList(),
            resultsDigestSentUtc: null);

        var teamNames = fixtures.ToDictionary(
            f => f.Id,
            f => new RoundFixtureTeams(f.HomeTeamName, f.AwayTeamName));

        return new RoundCompletionData(round, teamNames, participantsTask.Result, predictionsTask.Result);
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
        const string sql = @"
            SELECT
                m.[Id],
                m.[RoundId],
                m.[HomeTeamId],
                m.[AwayTeamId],
                m.[MatchDateTimeUtc],
                m.[CustomLockTimeUtc],
                m.[Status],
                m.[MatchNumber],
                ht.[Name] AS [HomeTeamName],
                at.[Name] AS [AwayTeamName]
            FROM
                [Matches] m
            LEFT JOIN
                [Teams] ht ON ht.[Id] = m.[HomeTeamId]
            LEFT JOIN
                [Teams] at ON at.[Id] = m.[AwayTeamId]
            WHERE
                m.[RoundId] = @RoundId;";

        return (await dbConnection.QueryAsync<FixtureRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }

    private async Task<IReadOnlyList<RoundParticipantRow>> GetParticipantsAsync(
        int roundId, int? leagueId, CancellationToken cancellationToken)
    {
        // DISTINCT because a player in two leagues in the same season appears once. LastRemindedUtc is
        // per (round, user), so the join cannot multiply it.
        const string sql = @"
            SELECT DISTINCT
                u.[Id] AS [UserId],
                u.[FirstName],
                u.[LastName],
                u.[Email],
                rn.[LastRemindedUtc]
            FROM
                [AspNetUsers] u
            INNER JOIN
                [LeagueMembers] lm ON u.[Id] = lm.[UserId] AND lm.[Status] = @ApprovedStatus
            INNER JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            INNER JOIN
                [Rounds] r ON l.[SeasonId] = r.[SeasonId]
            LEFT JOIN
                [PredictionReminderNotifications] rn ON rn.[RoundId] = r.[Id] AND rn.[UserId] = u.[Id]
            WHERE
                r.[Id] = @RoundId
                AND (@LeagueId IS NULL OR l.[Id] = @LeagueId);";

        return (await dbConnection.QueryAsync<RoundParticipantRow>(
            sql, cancellationToken,
            new { RoundId = roundId, LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    private async Task<IReadOnlyList<RoundPredictionRow>> GetPredictionsAsync(
        int roundId, int seasonId, int? leagueId, CancellationToken cancellationToken)
    {
        // Scoped to the same participants, so a prediction by someone outside the league cannot count.
        const string sql = @"
            SELECT DISTINCT
                up.[UserId],
                up.[MatchId]
            FROM
                [UserPredictions] up
            INNER JOIN
                [Matches] m ON m.[Id] = up.[MatchId]
            INNER JOIN
                [LeagueMembers] lm ON lm.[UserId] = up.[UserId] AND lm.[Status] = @ApprovedStatus
            INNER JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id] AND l.[SeasonId] = @SeasonId
            WHERE
                m.[RoundId] = @RoundId
                AND (@LeagueId IS NULL OR l.[Id] = @LeagueId);";

        return (await dbConnection.QueryAsync<RoundPredictionRow>(
            sql, cancellationToken,
            new
            {
                RoundId = roundId,
                LeagueId = leagueId,
                SeasonId = seasonId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            })).ToList();
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
        int? MatchNumber,
        string? HomeTeamName,
        string? AwayTeamName);
}

using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Persistence.Conformance;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;

/// <summary>
/// The SQL Server half of <see cref="ITestDataInspector"/>. Raw T-SQL on purpose: an assertion that read
/// through the code under test could be fooled by it, and there is no dialect-free way to read a row.
/// Statuses are stored as their enum name, so they are parsed back here rather than in the tests.
/// </summary>
internal sealed class SqlServerTestDataInspector(IDbConnectionFactory connectionFactory) : ITestDataInspector
{
    public async Task<IReadOnlyList<int>> MatchIdsForRoundAsync(int roundId)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<int>(
            "SELECT m.[Id] FROM [Matches] m WHERE m.[RoundId] = @RoundId;",
            new { RoundId = roundId })).ToList();
    }

    public async Task<int> PredictionCountForMatchAsync(int matchId)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM [UserPredictions] up WHERE up.[MatchId] = @MatchId;",
            new { MatchId = matchId });
    }

    public async Task<bool> MatchExistsAsync(int matchId)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM [Matches] m WHERE m.[Id] = @MatchId;",
            new { MatchId = matchId }) > 0;
    }

    public async Task<int?> RoundIdForMatchAsync(int matchId)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int?>(
            "SELECT m.[RoundId] FROM [Matches] m WHERE m.[Id] = @MatchId;",
            new { MatchId = matchId });
    }

    public async Task<StoredMatch?> MatchAsync(int matchId)
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
                m.[ExternalId],
                m.[MatchNumber]
            FROM
                [Matches] m
            WHERE
                m.[Id] = @MatchId;";

        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<MatchRow>(sql, new { MatchId = matchId });

        return row == null
            ? null
            : new StoredMatch(
                row.Id, row.RoundId, row.HomeTeamId, row.AwayTeamId, row.MatchDateTimeUtc,
                row.CustomLockTimeUtc, Enum.Parse<MatchStatus>(row.Status), row.ExternalId, row.MatchNumber);
    }

    public async Task<StoredRound?> RoundAsync(int roundId)
    {
        const string sql = @"
            SELECT
                r.[Id],
                r.[RoundNumber],
                r.[DisplayName],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                r.[ApiRoundName]
            FROM
                [Rounds] r
            WHERE
                r.[Id] = @RoundId;";

        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<RoundRow>(sql, new { RoundId = roundId });

        return row == null
            ? null
            : new StoredRound(
                row.Id, row.RoundNumber, row.DisplayName, row.StartDateUtc, row.DeadlineUtc,
                Enum.Parse<RoundStatus>(row.Status), row.ApiRoundName);
    }

    // Column order matches each SELECT above, per the Dapper result-mapping rule in CLAUDE.md.
    private sealed record MatchRow(
        int Id,
        int RoundId,
        int? HomeTeamId,
        int? AwayTeamId,
        DateTime MatchDateTimeUtc,
        DateTime? CustomLockTimeUtc,
        string Status,
        int? ExternalId,
        int? MatchNumber);

    private sealed record RoundRow(
        int Id,
        int RoundNumber,
        string DisplayName,
        DateTime StartDateUtc,
        DateTime DeadlineUtc,
        string Status,
        string? ApiRoundName);
}

using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class RoundRepository : IRoundRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    #region SQL Constants

    private const string AddMatchSql = @"
        INSERT INTO [Matches] 
        (
            [RoundId], 
            [HomeTeamId], 
            [AwayTeamId], 
            [MatchDateTime], 
            [Status],
            [ExternalId]
        )
        VALUES 
        (
            @RoundId, 
            @HomeTeamId, 
            @AwayTeamId, 
            @MatchDateTime, 
            @Status,
            @ExternalId
        );";

    private const string GetRoundsWithMatchesSql = @"
        SELECT 
            r.*, 
            m.*
        FROM [Rounds] r
        LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]";

    #endregion

    public RoundRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Create

    public async Task<Round> CreateAsync(Round round, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [Rounds] ([SeasonId], [RoundNumber], [StartDate], [Deadline], [ApiRoundName], [LastReminderSent])
            VALUES (@SeasonId, @RoundNumber, @StartDate, @Deadline, @ApiRoundName, @LastReminderSent);
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                round.SeasonId,
                round.RoundNumber,
                round.StartDate,
                round.Deadline,
                round.ApiRoundName,
                round.LastReminderSent
            },
            cancellationToken: cancellationToken
        );

        var newRoundId = await Connection.ExecuteScalarAsync<int>(command);

        if (round.Matches.Any())
        {
            var matchesToInsert = round.Matches.Select(m => new
            {
                RoundId = newRoundId,
                m.HomeTeamId,
                m.AwayTeamId,
                m.MatchDateTime,
                Status = m.Status.ToString()
            }).ToList();

            var insertMatchesCommand = new CommandDefinition(
                commandText: AddMatchSql,
                parameters: matchesToInsert,
                cancellationToken: cancellationToken
            );

            await Connection.ExecuteAsync(insertMatchesCommand);
        }

        typeof(Round).GetProperty(nameof(Round.Id))?.SetValue(round, newRoundId);
        return round;
    }

    #endregion

    #region Read

    public async Task<Round?> GetByIdAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Id] = @RoundId;";
        return await QueryAndMapRound(sql, cancellationToken, new { RoundId = roundId });
    }

    public async Task<Round?> GetByApiRoundNameAsync(int seasonId, string apiRoundName, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[SeasonId] = @SeasonId AND r.[ApiRoundName] = @ApiRoundName;";
        return await QueryAndMapRound(sql, cancellationToken, new { SeasonId = seasonId, ApiRoundName = apiRoundName });
    }

    public async Task<Round?> GetOldestInProgressRoundAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Id] = (SELECT TOP 1 [Id] FROM [Rounds] WHERE [SeasonId] = @SeasonId AND [Status] != @CompletedStatus AND [StartDate] < GETDATE() ORDER BY [StartDate] ASC)";
        return await QueryAndMapRound(sql, cancellationToken, new { SeasonId = seasonId, CompletedStatus = nameof(RoundStatus.Completed) });
    }

    public async Task<IEnumerable<int>> GetMatchIdsWithPredictionsAsync(IEnumerable<int> matchIds, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT DISTINCT
                [MatchId]
            FROM
                [UserPredictions]
            WHERE
                [MatchId] IN @MatchIds;
        ";

        var matchIdsList = matchIds.ToList();
        if (!matchIdsList.Any())
            return [];

        var command = new CommandDefinition(
            sql,
            new { MatchIds = matchIdsList },
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<int>(command);
    }

    public async Task<bool> IsLastRoundOfMonthAsync(int roundId, int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
        WITH RoundMonth AS (
            SELECT 
                MONTH([StartDate]) AS TargetMonth,
                YEAR([StartDate]) AS TargetYear
            FROM [Rounds]
            WHERE [Id] = @RoundId
        )
        SELECT 
            CASE WHEN @RoundId = (
                SELECT TOP 1 [Id]
                FROM [Rounds]
                WHERE 
                    [SeasonId] = @SeasonId 
                    AND MONTH([StartDate]) = (SELECT TargetMonth FROM RoundMonth) 
                    AND YEAR([StartDate]) = (SELECT TargetYear FROM RoundMonth)
                ORDER BY [StartDate] DESC
            ) THEN 1 ELSE 0 END;";

        var command = new CommandDefinition(
            sql,
            new { roundId, seasonId },
            cancellationToken: cancellationToken
        );

        return await Connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<bool> IsLastRoundOfSeasonAsync(int roundId, int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT 
                CASE WHEN r.[RoundNumber] = s.[NumberOfRounds] THEN 1 ELSE 0 END
            FROM 
                [dbo].[Rounds] r
            INNER JOIN 
                [dbo].[Seasons] s ON r.SeasonId = s.Id
            WHERE 
                r.Id = @RoundId 
                AND r.SeasonId = @SeasonId;";

        var command = new CommandDefinition(
            sql,
            new { RoundId = roundId, SeasonId = seasonId },
            cancellationToken: cancellationToken
        );

        return await Connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<IEnumerable<Match>> GetAllMatchesForMonthAsync(int month, int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                m.*
            FROM
                [Matches] m
            WHERE
                m.[RoundId] IN (
                    SELECT 
                        r.[Id] 
                    FROM 
                        [Rounds] r 
                    WHERE 
                        r.[SeasonId] = @SeasonId AND 
                        MONTH(r.[StartDate]) = @Month
                );";

        var command = new CommandDefinition(
            sql,
            new { month, seasonId },
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<Match>(command);
    }

    public async Task<Round?> GetNextRoundForReminderAsync(CancellationToken cancellationToken)
    {
        const string sqlWithMatches = @"
            WITH NextRound AS (
                SELECT TOP 1 [Id]
                FROM [Rounds]
                WHERE [Status] = @PublishedStatus
                AND [Deadline] > GETUTCDATE()
                ORDER BY [Deadline] ASC
            )

            SELECT 
                r.*, 
                m.*
            FROM [Rounds] r
            LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]
            WHERE r.[Id] IN (SELECT [Id] FROM NextRound);";

        return await QueryAndMapRound(sqlWithMatches, cancellationToken, new { PublishedStatus = nameof(RoundStatus.Published) });
    }

    public async Task<Dictionary<int, Round>> GetDraftRoundsStartingBeforeAsync(DateTime dateLimit, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Status] = @DraftStatus AND r.[StartDate] <= @DateLimit";
        return await QueryAndMapRounds(sql, cancellationToken, new { DraftStatus = nameof(RoundStatus.Draft), DateLimit = dateLimit });
    }

    #endregion

    #region Update

    public async Task UpdateAsync(Round round, CancellationToken cancellationToken)
    {
        const string updateRoundSql = @"
            UPDATE 
                [Rounds]
            SET 
                [RoundNumber] = @RoundNumber,
                [StartDate] = @StartDate,
                [Deadline] = @Deadline,
                [Status] = @Status,
                [ApiRoundName] = @ApiRoundName,
                [LastReminderSent] = @LastReminderSent
            WHERE 
                [Id] = @Id;";

        var updateRoundCommand = new CommandDefinition(updateRoundSql, new
        {
            round.Id,
            round.RoundNumber,
            round.StartDate,
            round.Deadline,
            Status = round.Status.ToString(),
            round.ApiRoundName,
            round.LastReminderSent
        }, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(updateRoundCommand);

        var existingMatchIdsCommand = new CommandDefinition("SELECT [Id] FROM [Matches] WHERE [RoundId] = @RoundId", new { RoundId = round.Id }, cancellationToken: cancellationToken);
        var existingMatchIds = (await Connection.QueryAsync<int>(existingMatchIdsCommand)).ToList();
        var incomingMatches = round.Matches.ToList();

        var matchesToInsert = incomingMatches.Where(m => m.Id == 0).ToList();
        var matchesToUpdate = incomingMatches.Where(m => m.Id != 0).ToList();
        var matchIdsToDelete = existingMatchIds.Except(incomingMatches.Select(m => m.Id)).ToList();

        if (matchesToInsert.Any())
        {
            var insertMatchesCommand = new CommandDefinition(AddMatchSql, matchesToInsert.Select(m => new
            {
                RoundId = round.Id,
                m.HomeTeamId,
                m.AwayTeamId,
                m.MatchDateTime,
                Status = m.Status.ToString(),
                m.ExternalId
            }), cancellationToken: cancellationToken);
            await Connection.ExecuteAsync(insertMatchesCommand);
        }

        if (matchesToUpdate.Any())
        {
            const string updateSql = @"
                UPDATE 
                    [Matches]
                SET
                    [HomeTeamId] = @HomeTeamId,
                    [AwayTeamId] = @AwayTeamId,
                    [MatchDateTime] = @MatchDateTime,
                    [ExternalId] = @ExternalId
                WHERE
                    [Id] = @Id;";

            var updateMatchesCommand = new CommandDefinition(updateSql, matchesToUpdate, cancellationToken: cancellationToken);
            await Connection.ExecuteAsync(updateMatchesCommand);
        }

        if (matchIdsToDelete.Any())
        {
            const string deleteSql = "DELETE FROM [Matches] WHERE [Id] IN @MatchIdsToDelete;";
            var deleteMatchesCommand = new CommandDefinition(deleteSql, new { MatchIdsToDelete = matchIdsToDelete }, cancellationToken: cancellationToken);
            await Connection.ExecuteAsync(deleteMatchesCommand);
        }
    }

    public async Task UpdateLastReminderSentAsync(Round round, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE 
                [Rounds]
            SET 
                [LastReminderSent] = @LastReminderSent
            WHERE 
                [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                round.Id,
                round.LastReminderSent
            },
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateMatchScoresAsync(List<Match> matches, CancellationToken cancellationToken)
    {
        if (!matches.Any())
            return;

        const string sql = @"
        UPDATE
            [Matches]
        SET
            [ActualHomeTeamScore] = @ActualHomeTeamScore,
            [ActualAwayTeamScore] = @ActualAwayTeamScore,
            [Status] = @Status
        WHERE
            [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: matches.Select(m => new
            {
                m.Id,
                m.ActualHomeTeamScore,
                m.ActualAwayTeamScore,
                Status = m.Status.ToString()
            }),
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateRoundResultsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            MERGE [RoundResults] AS target
            USING (
                SELECT
                    m.[RoundId],
                    up.[UserId],
                    SUM(ISNULL(up.[PointsAwarded], 0)) AS [TotalPoints],
                    SUM(CASE WHEN up.[PointsAwarded] = 5 THEN 1 ELSE 0 END) AS [ExactScoreCount],
                    SUM(CASE WHEN up.[PointsAwarded] >= 3 THEN 1 ELSE 0 END) AS [CorrectResultCount]
                FROM [UserPredictions] up
                INNER JOIN [Matches] m ON m.[Id] = up.[MatchId]
                WHERE m.[RoundId] = @RoundId
                AND up.[PointsAwarded] IS NOT NULL
                GROUP BY
                    m.[RoundId],
                    up.[UserId]
            ) AS src
            ON target.[RoundId] = src.[RoundId]
            AND target.[UserId] = src.[UserId]
            WHEN MATCHED THEN
                UPDATE SET 
                    target.[TotalPoints]        = src.[TotalPoints],
                    target.[ExactScoreCount]    = src.[ExactScoreCount],
                    target.[CorrectResultCount] = src.[CorrectResultCount]
            WHEN NOT MATCHED BY TARGET THEN
                INSERT ([RoundId], [UserId], [TotalPoints], [ExactScoreCount], [CorrectResultCount])
                VALUES (src.[RoundId], src.[UserId], src.[TotalPoints], src.[ExactScoreCount], src.[CorrectResultCount]);";

        var command = new CommandDefinition(
            sql,
            new { RoundId = roundId },
            cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Private Helper Methods

    private async Task<Round?> QueryAndMapRound(string sql, CancellationToken cancellationToken, object? param = null)
    {
        return (await QueryAndMapRounds(sql, cancellationToken, param)).Values.FirstOrDefault();
    }

    private async Task<Dictionary<int, Round>> QueryAndMapRounds(string sql, CancellationToken cancellationToken, object? param = null)
    {
        var command = new CommandDefinition(
            commandText: sql,
            parameters: param,
            cancellationToken: cancellationToken
        );

        var queryResult = await Connection.QueryAsync<Round, Match?, (Round Round, Match? Match)>(
            command,
            (round, match) => (round, match),
            splitOn: "Id"
        );

        var groupedResult = queryResult
            .GroupBy(p => p.Round.Id)
            .Select(g =>
            {
                var groupedRound = g.First().Round;
                var matches = g.Select(p => p.Match).Where(m => m != null).ToList();

                return new Round(
                    groupedRound.Id,
                    groupedRound.SeasonId,
                    groupedRound.RoundNumber,
                    groupedRound.StartDate,
                    groupedRound.Deadline,
                    groupedRound.Status,
                    groupedRound.ApiRoundName,
                    groupedRound.LastReminderSent,
                    matches
                );
            });

        return groupedResult.ToDictionary(r => r.Id);
    }

    #endregion
}
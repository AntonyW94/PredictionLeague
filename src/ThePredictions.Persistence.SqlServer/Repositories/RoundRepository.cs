using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class RoundRepository(
    IDbConnectionFactory connectionFactory,
    IDbTransactionContext transactionContext,
    IDateTimeProvider dateTimeProvider)
    : RepositoryBase(connectionFactory, transactionContext), IRoundRepository
{
    #region SQL Constants

    /// <remarks>
    /// Every fixture in one statement. The <c>WITH</c> clause names each column's type, and the JSON is matched
    /// to it by property name rather than by position, so the two call sites building the row objects may list
    /// their properties in any order.
    /// </remarks>
    private const string AddMatchSql = @"
        INSERT INTO [Matches]
        (
            [RoundId],
            [HomeTeamId],
            [AwayTeamId],
            [MatchDateTimeUtc],
            [CustomLockTimeUtc],
            [Status],
            [ExternalId],
            [MatchNumber],
            [PlaceholderHomeName],
            [PlaceholderAwayName],
            [ApiRoundName]
        )
        SELECT
            src.[RoundId],
            src.[HomeTeamId],
            src.[AwayTeamId],
            src.[MatchDateTimeUtc],
            src.[CustomLockTimeUtc],
            src.[Status],
            src.[ExternalId],
            src.[MatchNumber],
            src.[PlaceholderHomeName],
            src.[PlaceholderAwayName],
            src.[ApiRoundName]
        FROM
            OPENJSON(@Rows)
            WITH (
                [RoundId] int 'strict $.RoundId',
                [HomeTeamId] int 'strict $.HomeTeamId',
                [AwayTeamId] int 'strict $.AwayTeamId',
                [MatchDateTimeUtc] datetime2 'strict $.MatchDateTimeUtc',
                [CustomLockTimeUtc] datetime2 'strict $.CustomLockTimeUtc',
                [Status] nvarchar(4000) 'strict $.Status',
                [ExternalId] int 'strict $.ExternalId',
                [MatchNumber] int 'strict $.MatchNumber',
                [PlaceholderHomeName] nvarchar(4000) 'strict $.PlaceholderHomeName',
                [PlaceholderAwayName] nvarchar(4000) 'strict $.PlaceholderAwayName',
                [ApiRoundName] nvarchar(4000) 'strict $.ApiRoundName'
            ) src;";

    private const string GetRoundsWithMatchesSql = @"
        SELECT
            r.*,
            m.*
        FROM [Rounds] r
        LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]";

    #endregion

    #region Create

    public async Task<Round> CreateAsync(Round round, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [Rounds] ([SeasonId], [RoundNumber], [DisplayName], [StartDateUtc], [DeadlineUtc], [ApiRoundName], [LastReminderSentUtc])
            VALUES (@SeasonId, @RoundNumber, @DisplayName, @StartDateUtc, @DeadlineUtc, @ApiRoundName, @LastReminderSentUtc);
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                round.SeasonId,
                round.RoundNumber,
                round.DisplayName,
                round.StartDateUtc,
                round.DeadlineUtc,
                round.ApiRoundName,
                round.LastReminderSentUtc
            },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        var newRoundId = await Connection.ExecuteScalarAsync<int>(command);

        if (!round.Matches.Any())
        {
            return new Round(
                id: newRoundId,
                seasonId: round.SeasonId,
                roundNumber: round.RoundNumber,
                displayName: round.DisplayName,
                startDateUtc: round.StartDateUtc,
                deadlineUtc: round.DeadlineUtc,
                status: round.Status,
                apiRoundName: round.ApiRoundName,
                lastReminderSentUtc: round.LastReminderSentUtc,
                matches: round.Matches
            );
        }

        var matchesToInsert = round.Matches.Select(m => new
        {
            RoundId = newRoundId,
            m.HomeTeamId,
            m.AwayTeamId,
            m.MatchDateTimeUtc,
            m.CustomLockTimeUtc,
            Status = m.Status.ToString(),
            m.ExternalId,
            m.MatchNumber,
            m.PlaceholderHomeName,
            m.PlaceholderAwayName,
            m.ApiRoundName
        }).ToList();

        var insertMatchesCommand = new CommandDefinition(
            commandText: AddMatchSql,
            parameters: new { Rows = JsonRows.From(matchesToInsert) },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(insertMatchesCommand);

        return new Round(
            id: newRoundId,
            seasonId: round.SeasonId,
            roundNumber: round.RoundNumber,
            displayName: round.DisplayName,
            startDateUtc: round.StartDateUtc,
            deadlineUtc: round.DeadlineUtc,
            status: round.Status,
            apiRoundName: round.ApiRoundName,
            lastReminderSentUtc: round.LastReminderSentUtc,
            matches: round.Matches
        );
    }

    #endregion

    #region Read

    public async Task<Round?> GetByIdAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Id] = @RoundId;";
        return await QueryAndMapRoundAsync(sql, cancellationToken, new { RoundId = roundId });
    }

    public async Task<Dictionary<int, Round>> GetAllForSeasonAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[SeasonId] = @SeasonId;";
        return await QueryAndMapRoundsAsync(sql, cancellationToken, new { SeasonId = seasonId });
    }

    public async Task<Round?> GetByApiRoundNameAsync(int seasonId, string apiRoundName, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[SeasonId] = @SeasonId AND r.[ApiRoundName] = @ApiRoundName;";
        return await QueryAndMapRoundAsync(sql, cancellationToken, new { SeasonId = seasonId, ApiRoundName = apiRoundName });
    }

    public async Task<Round?> GetOldestInProgressRoundAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Id] = (SELECT TOP 1 [Id] FROM [Rounds] WHERE [SeasonId] = @SeasonId AND [Status] IN (@PublishedStatus, @InProgressStatus) AND [StartDateUtc] < @NowUtc ORDER BY [StartDateUtc] ASC)";
        return await QueryAndMapRoundAsync(sql, cancellationToken, new
        {
            SeasonId = seasonId,
            PublishedStatus = nameof(RoundStatus.Published),
            InProgressStatus = nameof(RoundStatus.InProgress),
            NowUtc = dateTimeProvider.UtcNow
        });
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
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<int>(command);
    }

    public async Task<bool> IsLastRoundOfMonthAsync(int roundId, int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
        WITH RoundMonth AS (
            SELECT
                MONTH([StartDateUtc]) AS TargetMonth,
                YEAR([StartDateUtc]) AS TargetYear
            FROM [Rounds]
            WHERE [Id] = @RoundId
        )
        SELECT
            CASE WHEN @RoundId = (
                SELECT TOP 1 [Id]
                FROM [Rounds]
                WHERE
                    [SeasonId] = @SeasonId
                    AND MONTH([StartDateUtc]) = (SELECT [TargetMonth] FROM [RoundMonth])
                    AND YEAR([StartDateUtc]) = (SELECT [TargetYear] FROM [RoundMonth])
                ORDER BY [StartDateUtc] DESC
            ) THEN 1 ELSE 0 END;";

        var command = new CommandDefinition(
            sql,
            new { roundId, seasonId },
            transaction: Transaction,
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
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<IEnumerable<int>> GetRoundsIdsForMonthAsync(int month, int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id]
            FROM
                [Rounds] r
            WHERE
                r.[SeasonId] = @SeasonId
                AND MONTH(r.[StartDateUtc]) = @Month";

        var command = new CommandDefinition(
            sql,
            new
            {
                Month = month,
                SeasonId = seasonId
            },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QueryAsync<int>(command);
    }

    public async Task<Round?> GetNextRoundForReminderAsync(CancellationToken cancellationToken)
    {
        // Pick the round whose next batch locks soonest. A round is a candidate while it still has a
        // predictable match that has not locked - the effective lock being the per-match CustomLockTimeUtc
        // or the round deadline. In-progress rounds are included because a combined round can still have a
        // later batch (its final and third-place playoff) open after its earlier matches have kicked off.
        const string sqlWithMatches = @"
            WITH NextRound AS (
                SELECT TOP 1 r.[Id]
                FROM [Rounds] r
                WHERE r.[Status] IN (@PublishedStatus, @InProgressStatus)
                    AND EXISTS (
                        SELECT 1
                        FROM [Matches] m
                        WHERE m.[RoundId] = r.[Id]
                            AND m.[HomeTeamId] IS NOT NULL
                            AND m.[AwayTeamId] IS NOT NULL
                            AND m.[Status] <> @PostponedStatus
                            AND COALESCE(m.[CustomLockTimeUtc], r.[DeadlineUtc]) > @NowUtc
                    )
                ORDER BY
                    CASE
                        WHEN r.[DeadlineUtc] > @NowUtc THEN r.[DeadlineUtc]
                        ELSE COALESCE(
                            (
                                SELECT MIN(m.[CustomLockTimeUtc])
                                FROM [Matches] m
                                WHERE m.[RoundId] = r.[Id]
                                    AND m.[HomeTeamId] IS NOT NULL
                                    AND m.[AwayTeamId] IS NOT NULL
                                    AND m.[Status] <> @PostponedStatus
                                    AND m.[CustomLockTimeUtc] > @NowUtc
                            ),
                            r.[DeadlineUtc])
                    END ASC
            )

            SELECT
                r.*,
                m.*
            FROM [Rounds] r
            LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]
            WHERE r.[Id] IN (SELECT [Id] FROM NextRound);";

        return await QueryAndMapRoundAsync(
            sqlWithMatches,
            cancellationToken,
            new
            {
                PublishedStatus = nameof(RoundStatus.Published),
                InProgressStatus = nameof(RoundStatus.InProgress),
                PostponedStatus = nameof(MatchStatus.Postponed),

                // One instant for all three comparisons. Three separate GETUTCDATE() calls could each land on a different
                // millisecond, so a fixture locking at that moment could be open in one clause and closed in the next.
                NowUtc = dateTimeProvider.UtcNow
            });
    }

    public async Task<Dictionary<int, Round>> GetDraftRoundsStartingBeforeAsync(DateTime dateLimitUtc, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Status] = @DraftStatus AND r.[StartDateUtc] <= @DateLimit";
        return await QueryAndMapRoundsAsync(sql, cancellationToken, new { DraftStatus = nameof(RoundStatus.Draft), DateLimit = dateLimitUtc });
    }

    public async Task<Dictionary<int, Round>> GetPublishedRoundsStartingAfterAsync(DateTime dateLimitUtc, CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Status] = @PublishedStatus AND r.[StartDateUtc] > @DateLimit";
        return await QueryAndMapRoundsAsync(sql, cancellationToken, new { PublishedStatus = nameof(RoundStatus.Published), DateLimit = dateLimitUtc });
    }

    public async Task<Dictionary<int, Round>> GetPublishedRoundsAsync(CancellationToken cancellationToken)
    {
        const string sql = $"{GetRoundsWithMatchesSql} WHERE r.[Status] = @PublishedStatus";
        return await QueryAndMapRoundsAsync(sql, cancellationToken, new { PublishedStatus = nameof(RoundStatus.Published) });
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
                [DisplayName] = @DisplayName,
                [StartDateUtc] = @StartDateUtc,
                [DeadlineUtc] = @DeadlineUtc,
                [CompletedDateUtc] = @CompletedDateUtc,
                [Status] = @Status,
                [ApiRoundName] = @ApiRoundName,
                [LastReminderSentUtc] = @LastReminderSentUtc
            WHERE
                [Id] = @Id;";

        var updateRoundCommand = new CommandDefinition(updateRoundSql, new
        {
            round.Id,
            round.RoundNumber,
            round.DisplayName,
            round.StartDateUtc,
            round.DeadlineUtc,
            round.CompletedDateUtc,
            Status = round.Status.ToString(),
            round.ApiRoundName,
            round.LastReminderSentUtc
        }, transaction: Transaction, cancellationToken: cancellationToken);
        await Connection.ExecuteAsync(updateRoundCommand);

        var existingMatchIdsCommand = new CommandDefinition("SELECT [Id] FROM [Matches] WHERE [RoundId] = @RoundId", new { RoundId = round.Id }, transaction: Transaction, cancellationToken: cancellationToken);
        var existingMatchIds = (await Connection.QueryAsync<int>(existingMatchIdsCommand)).ToList();
        var incomingMatches = round.Matches.ToList();

        var matchesToInsert = incomingMatches.Where(m => m.Id == 0).ToList();
        var matchesToUpdate = incomingMatches.Where(m => m.Id != 0).ToList();
        var matchIdsToDelete = existingMatchIds.Except(incomingMatches.Select(m => m.Id)).ToList();

        if (matchesToInsert.Any())
        {
            var rowsToInsert = matchesToInsert.Select(m => new
            {
                RoundId = round.Id,
                m.HomeTeamId,
                m.AwayTeamId,
                m.MatchDateTimeUtc,
                m.CustomLockTimeUtc,
                Status = m.Status.ToString(),
                m.ExternalId,
                m.MatchNumber,
                m.PlaceholderHomeName,
                m.PlaceholderAwayName,
                m.ApiRoundName
            }).ToList();

            var insertMatchesCommand = new CommandDefinition(
                AddMatchSql,
                new { Rows = JsonRows.From(rowsToInsert) },
                transaction: Transaction,
                cancellationToken: cancellationToken);

            await Connection.ExecuteAsync(insertMatchesCommand);
        }

        if (matchesToUpdate.Any())
        {
            const string updateSql = @"
                UPDATE
                    m
                SET
                    m.[RoundId] = src.[RoundId],
                    m.[HomeTeamId] = src.[HomeTeamId],
                    m.[AwayTeamId] = src.[AwayTeamId],
                    m.[MatchDateTimeUtc] = src.[MatchDateTimeUtc],
                    m.[CustomLockTimeUtc] = src.[CustomLockTimeUtc],
                    m.[ExternalId] = src.[ExternalId],
                    m.[MatchNumber] = src.[MatchNumber],
                    m.[Status] = src.[Status],
                    m.[PlaceholderHomeName] = src.[PlaceholderHomeName],
                    m.[PlaceholderAwayName] = src.[PlaceholderAwayName],
                    m.[ApiRoundName] = src.[ApiRoundName]
                FROM
                    [Matches] m
                INNER JOIN
                    OPENJSON(@Rows)
                    WITH (
                        [Id] int 'strict $.Id',
                        [RoundId] int 'strict $.RoundId',
                        [HomeTeamId] int 'strict $.HomeTeamId',
                        [AwayTeamId] int 'strict $.AwayTeamId',
                        [MatchDateTimeUtc] datetime2 'strict $.MatchDateTimeUtc',
                        [CustomLockTimeUtc] datetime2 'strict $.CustomLockTimeUtc',
                        [ExternalId] int 'strict $.ExternalId',
                        [MatchNumber] int 'strict $.MatchNumber',
                        [Status] nvarchar(4000) 'strict $.Status',
                        [PlaceholderHomeName] nvarchar(4000) 'strict $.PlaceholderHomeName',
                        [PlaceholderAwayName] nvarchar(4000) 'strict $.PlaceholderAwayName',
                        [ApiRoundName] nvarchar(4000) 'strict $.ApiRoundName'
                    ) src ON src.[Id] = m.[Id];";

            var rowsToUpdate = matchesToUpdate.Select(m => new
            {
                m.Id,
                m.RoundId,
                m.HomeTeamId,
                m.AwayTeamId,
                m.MatchDateTimeUtc,
                m.CustomLockTimeUtc,
                m.ExternalId,
                m.MatchNumber,
                Status = m.Status.ToString(),
                m.PlaceholderHomeName,
                m.PlaceholderAwayName,
                m.ApiRoundName
            }).ToList();

            var updateMatchesCommand = new CommandDefinition(
                updateSql,
                new { Rows = JsonRows.From(rowsToUpdate) },
                transaction: Transaction,
                cancellationToken: cancellationToken);

            await Connection.ExecuteAsync(updateMatchesCommand);
        }

        if (matchIdsToDelete.Any())
        {
            const string deleteSql = @"
                DELETE FROM [Matches]
                WHERE
                    [Id] IN @MatchIdsToDelete
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [UserPredictions] up
                        WHERE up.[MatchId] = [Matches].[Id]
                    );";

            var deleteMatchesCommand = new CommandDefinition(deleteSql, new { MatchIdsToDelete = matchIdsToDelete }, transaction: Transaction, cancellationToken: cancellationToken);
            await Connection.ExecuteAsync(deleteMatchesCommand);
        }
    }

    public async Task MoveMatchesToRoundAsync(IEnumerable<int> matchIds, int targetRoundId, CancellationToken cancellationToken)
    {
        var matchIdsList = matchIds.ToList();
        if (!matchIdsList.Any())
            return;

        const string sql = @"
            UPDATE
                [Matches]
            SET
                [RoundId] = @TargetRoundId
            WHERE
                [Id] IN @MatchIds;";

        var command = new CommandDefinition(
            sql,
            new { TargetRoundId = targetRoundId, MatchIds = matchIdsList },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateLastReminderSentAsync(Round round, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                [Rounds]
            SET
                [LastReminderSentUtc] = @LastReminderSentUtc
            WHERE
                [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                round.Id,
                round.LastReminderSentUtc
            },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateResultsDigestSentAsync(Round round, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                [Rounds]
            SET
                [ResultsDigestSentUtc] = @ResultsDigestSentUtc
            WHERE
                [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                round.Id,
                round.ResultsDigestSentUtc
            },
            transaction: Transaction,
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
            m
        SET
            m.[ActualHomeTeamScore] = src.[ActualHomeTeamScore],
            m.[ActualAwayTeamScore] = src.[ActualAwayTeamScore],
            m.[Status] = src.[Status]
        FROM
            [Matches] m
        INNER JOIN
            OPENJSON(@Rows)
            WITH (
                [Id] int 'strict $.Id',
                [ActualHomeTeamScore] int 'strict $.ActualHomeTeamScore',
                [ActualAwayTeamScore] int 'strict $.ActualAwayTeamScore',
                [Status] nvarchar(4000) 'strict $.Status'
            ) src ON src.[Id] = m.[Id];";

        var rows = matches
            .Select(m => new
            {
                m.Id,
                m.ActualHomeTeamScore,
                m.ActualAwayTeamScore,
                Status = m.Status.ToString()
            })
            .ToList();

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Rows = JsonRows.From(rows) },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateRoundResultsAsync(
        int roundId,
        IEnumerable<RoundResultTally> tallies,
        CancellationToken cancellationToken)
    {
        // One row per player, upserted. What the counts mean, and which predictions count towards them, is
        // Domain.Services.OutcomeTally - this only stores the answer.
        //
        // No WHEN NOT MATCHED BY SOURCE clause, so a player with no tally in this batch keeps the row they have. That is
        // how the statement this replaces behaved, and it matters: re-processing a round with a fixture reverted to
        // unplayed must not wipe the rest of the round's results.
        const string sql = @"
            MERGE [RoundResults] AS target
            USING (
                SELECT
                    src.[RoundId],
                    src.[UserId],
                    src.[ExactScoreCount],
                    src.[CorrectResultCount],
                    src.[IncorrectCount]
                FROM
                    OPENJSON(@Rows)
                    WITH (
                        [RoundId] int 'strict $.RoundId',
                        [UserId] nvarchar(4000) 'strict $.UserId',
                        [ExactScoreCount] int 'strict $.ExactScoreCount',
                        [CorrectResultCount] int 'strict $.CorrectResultCount',
                        [IncorrectCount] int 'strict $.IncorrectCount'
                    ) src
            ) AS src
            ON target.[RoundId] = src.[RoundId]
               AND target.[UserId] = src.[UserId]

            WHEN MATCHED THEN
                UPDATE SET
                    target.[ExactScoreCount]    = src.[ExactScoreCount],
                    target.[CorrectResultCount] = src.[CorrectResultCount],
                    target.[IncorrectCount]     = src.[IncorrectCount]

            WHEN NOT MATCHED BY TARGET THEN
                INSERT ([RoundId], [UserId], [ExactScoreCount], [CorrectResultCount], [IncorrectCount])
                VALUES (src.[RoundId], src.[UserId], src.[ExactScoreCount], src.[CorrectResultCount], src.[IncorrectCount]);";

        var rows = tallies
            .Select(tally => new
            {
                RoundId = roundId,
                tally.UserId,
                tally.Counts.ExactScoreCount,
                tally.Counts.CorrectResultCount,
                tally.Counts.IncorrectCount
            })
            .ToList();

        if (rows.Count == 0)
            return;

        var command = new CommandDefinition(
            sql,
            new { Rows = JsonRows.From(rows) },
            transaction: Transaction,
            cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Private Helper Methods

    private async Task<Round?> QueryAndMapRoundAsync(string sql, CancellationToken cancellationToken, object? param = null)
    {
        return (await QueryAndMapRoundsAsync(sql, cancellationToken, param)).Values.FirstOrDefault();
    }

    private async Task<Dictionary<int, Round>> QueryAndMapRoundsAsync(string sql, CancellationToken cancellationToken, object? param = null)
    {
        var command = new CommandDefinition(
            commandText: sql,
            parameters: param,
            transaction: Transaction,
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
                    groupedRound.DisplayName,
                    groupedRound.StartDateUtc,
                    groupedRound.DeadlineUtc,
                    groupedRound.Status,
                    groupedRound.ApiRoundName,
                    groupedRound.LastReminderSentUtc,
                    matches,
                    groupedRound.ResultsDigestSentUtc
                );
            });

        return groupedResult.ToDictionary(r => r.Id);
    }

    #endregion
}

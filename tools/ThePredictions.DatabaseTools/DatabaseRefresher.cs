using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ThePredictions.DatabaseTools;

public class DatabaseRefresher(
    string sourceConnectionString,
    string targetConnectionString,
    string? testPassword,
    bool anonymise,
    bool keepLeagueNames = false,
    bool preserveFirstNames = false)
{
    // NOTE: DbUp's [dbo].[SchemaVersions] migration journal is deliberately absent from BOTH
    // arrays below. Each database owns its own journal, so the data refresh/backup must neither
    // copy nor truncate it. Do not add it here. (See ADR-0013.)
    //
    // NOTE: [dbo].[EmailSettings] is also deliberately absent from BOTH arrays. It is the
    // per-environment master email switch, and each environment owns its own value: dev keeps
    // emails OFF so it never double-sends to the real inboxes a refresh preserves, while prod
    // stays ON. Copying it would overwrite dev's OFF with prod's default; listing it under
    // TablesToSkip would still TRUNCATE it (skip means "don't copy", not "don't touch"), and an
    // empty table reads as emails-ON. Leaving it out of both arrays preserves the target's row.
    private static readonly string[] TablesToSkip =
    [
        "AspNetUserTokens",
        "RefreshTokens",
        "PasswordResetTokens",
        "EmailConfirmationTokens"
    ];

    private static readonly string[] TableCopyOrder =
    [
        "AspNetRoles",
        "AspNetUsers",
        "AspNetUserRoles",
        "AspNetUserClaims",
        "AspNetRoleClaims",
        "AspNetUserLogins",
        "Teams",
        "Competitions",
        "RunningCosts",
        "PricingSettings",
        "ServiceFees",
        "Seasons",
        "SeasonPasses",
        "UserOnboardingSkips",
        "UserPayoutDetails",
        "TournamentRoundMappings",
        "Rounds",
        "Matches",
        "BoostDefinitions",
        "Leagues",
        "LeagueMembers",
        "LeagueMemberStats",
        "LeagueBoostRules",
        "LeagueBoostWindows",
        "LeaguePrizeSettings",
        "LeaguePrizeScheme",
        "LeaguePrizeSchemeEntries",
        "UserPredictions",
        "RoundResults",
        "LeagueRoundResults",
        "UserBoostUsages",
        "Winnings",
        "PrizeNotifications",
        "LeagueWelcomeNotifications",
        "PredictionReminderNotifications",
        "LeaguePayouts"
    ];

    private static readonly string[] AllTables = TableCopyOrder
        .Concat(TablesToSkip)
        .ToArray();

    public async Task RunAsync()
    {
        Console.WriteLine("[INFO] Starting database refresh...");
        Console.WriteLine($"[INFO] Anonymisation: {(anonymise ? "enabled" : "disabled")}");

        if (anonymise)
        {
            Console.WriteLine($"[INFO] League names: {(keepLeagueNames ? "kept" : "anonymised")}");
            Console.WriteLine($"[INFO] First names: {(preserveFirstNames ? "preserved" : "anonymised")}");
        }

        var tableData = new Dictionary<string, IEnumerable<dynamic>>();

        await using var sourceConnection = new SqlConnection(sourceConnectionString);
        await sourceConnection.OpenAsync();

        var sourceTableCheck = new HashSet<string>();
        foreach (var table in TableCopyOrder)
        {
            var exists = await sourceConnection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM [INFORMATION_SCHEMA].[TABLES] WHERE [TABLE_NAME] = @Table",
                new { Table = table });

            if (exists > 0)
                sourceTableCheck.Add(table);
        }

        foreach (var table in TableCopyOrder)
        {
            if (!sourceTableCheck.Contains(table))
            {
                Console.WriteLine($"[WARN] [{table}] does not exist on source, skipping");
                tableData[table] = [];
                continue;
            }

            Console.WriteLine($"[INFO] Reading [{table}] from source...");

            if (table == "AspNetUserLogins" && anonymise)
            {
                var preservedUserIds = await GetPreservedUserIdsAsync(sourceConnection);

                if (preservedUserIds.Count > 0)
                {
                    var rows = (await sourceConnection.QueryAsync(
                        $"""
                        SELECT
                            *
                        FROM
                            [{table}] l
                        WHERE
                            l.[UserId] IN @UserIds
                        """,
                        new { UserIds = preservedUserIds })).ToList();

                    tableData[table] = rows;
                    Console.WriteLine($"[INFO] Read {rows.Count} rows from [{table}] (preserved accounts only)");
                }
                else
                {
                    tableData[table] = [];
                    Console.WriteLine($"[INFO] No preserved accounts found, skipping [{table}]");
                }
            }
            else
            {
                var rows = (await sourceConnection.QueryAsync($"SELECT * FROM [{table}]")).ToList();
                tableData[table] = rows;
                Console.WriteLine($"[INFO] Read {rows.Count} rows from [{table}]");
            }
        }

        if (anonymise)
        {
            Console.WriteLine("[INFO] Anonymising personal data...");
            tableData["AspNetUsers"] = DataAnonymiser.AnonymiseUsers(tableData["AspNetUsers"], preserveFirstNames);
            tableData["Leagues"] = DataAnonymiser.AnonymiseLeagues(tableData["Leagues"], keepLeagueNames);
            tableData["SeasonPasses"] = DataAnonymiser.AnonymiseSeasonPasses(tableData["SeasonPasses"]);
            tableData["UserPayoutDetails"] = DataAnonymiser.AnonymiseUserPayoutDetails(tableData["UserPayoutDetails"]);
        }

        await using var targetConnection = new SqlConnection(targetConnectionString);
        await targetConnection.OpenAsync();

        Console.WriteLine("[INFO] Disabling foreign key constraints on target...");
        await DisableForeignKeyConstraintsAsync(targetConnection);

        try
        {
            Console.WriteLine("[INFO] Truncating target tables...");
            await TruncateAllTablesAsync(targetConnection);

            foreach (var table in TableCopyOrder)
            {
                var rows = tableData[table].ToList();

                if (rows.Count == 0)
                {
                    Console.WriteLine($"[INFO] [{table}] is empty, skipping insert");
                    continue;
                }

                Console.WriteLine($"[INFO] Inserting {rows.Count} rows into [{table}]...");
                await InsertRowsAsync(targetConnection, table, rows);
            }

            if (anonymise && testPassword is not null)
            {
                Console.WriteLine("[INFO] Creating test accounts...");
                var testAccountCreator = new TestAccountCreator(targetConnection, testPassword);
                await testAccountCreator.CreateTestAccountsAsync();
            }
        }
        finally
        {
            Console.WriteLine("[INFO] Re-enabling foreign key constraints on target...");
            await EnableForeignKeyConstraintsAsync(targetConnection);
        }

        if (anonymise)
        {
            Console.WriteLine("[INFO] Verifying no personal data remains...");
            var verifier = new PersonalDataVerifier(targetConnection);
            await verifier.VerifyAsync();
        }

        Console.WriteLine("[INFO] Database refresh completed.");
    }

    private static async Task<List<string>> GetPreservedUserIdsAsync(SqlConnection connection)
    {
        var ids = await connection.QueryAsync<string>(
            """
            SELECT
                u.[Id]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Email] IN @Emails
            """,
            new { Emails = DataAnonymiser.PreservedEmails });

        return ids.ToList();
    }

    private static async Task<HashSet<string>> GetTargetTablesAsync(SqlConnection connection)
    {
        var tables = await connection.QueryAsync<string>(
            "SELECT [TABLE_NAME] FROM [INFORMATION_SCHEMA].[TABLES] WHERE [TABLE_TYPE] = 'BASE TABLE'");

        return [..tables];
    }

    private static async Task DisableForeignKeyConstraintsAsync(SqlConnection connection)
    {
        var targetTables = await GetTargetTablesAsync(connection);

        foreach (var table in AllTables.Where(t => targetTables.Contains(t)))
        {
            await connection.ExecuteAsync($"ALTER TABLE [{table}] NOCHECK CONSTRAINT ALL");
        }
    }

    private static async Task EnableForeignKeyConstraintsAsync(SqlConnection connection)
    {
        var targetTables = await GetTargetTablesAsync(connection);

        foreach (var table in AllTables.Reverse().Where(t => targetTables.Contains(t)))
        {
            await connection.ExecuteAsync($"ALTER TABLE [{table}] WITH CHECK CHECK CONSTRAINT ALL");
        }
    }

    private static async Task TruncateAllTablesAsync(SqlConnection connection)
    {
        var targetTables = await GetTargetTablesAsync(connection);

        foreach (var table in AllTables.Reverse().Where(t => targetTables.Contains(t)))
        {
            await connection.ExecuteAsync($"DELETE FROM [{table}]");
        }
    }

    private static async Task<HashSet<string>> GetTargetColumnsAsync(SqlConnection connection, string table)
    {
        var columns = await connection.QueryAsync<string>(
            """
            SELECT
                c.[COLUMN_NAME]
            FROM
                [INFORMATION_SCHEMA].[COLUMNS] c
            WHERE
                c.[TABLE_NAME] = @Table
            """,
            new { Table = table });

        return [..columns];
    }

    private static async Task<bool> TableExistsOnTargetAsync(SqlConnection connection, string table)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT
                COUNT(*)
            FROM
                [INFORMATION_SCHEMA].[TABLES] t
            WHERE
                t.[TABLE_NAME] = @Table
            """,
            new { Table = table });

        return count > 0;
    }

    private static async Task InsertRowsAsync(SqlConnection connection, string table, List<dynamic> rows)
    {
        if (rows.Count == 0)
            return;

        if (!await TableExistsOnTargetAsync(connection, table))
        {
            Console.WriteLine($"[WARN] [{table}] does not exist on target, skipping");
            return;
        }

        var targetColumns = await GetTargetColumnsAsync(connection, table);

        var firstRow = (IDictionary<string, object?>)rows[0];
        var sourceColumns = firstRow.Keys.ToList();
        var columns = sourceColumns.Where(c => targetColumns.Contains(c)).ToList();

        var skippedColumns = sourceColumns.Except(columns).ToList();
        if (skippedColumns.Count > 0)
        {
            Console.WriteLine($"[WARN] [{table}] skipping columns not on target: {string.Join(", ", skippedColumns)}");
        }

        var dataTable = new DataTable();

        foreach (var column in columns)
        {
            var value = firstRow[column];
            var columnType = value?.GetType() ?? typeof(string);
            dataTable.Columns.Add(column, columnType);
        }

        foreach (var row in rows)
        {
            var dict = (IDictionary<string, object?>)row;
            var dataRow = dataTable.NewRow();

            foreach (var column in columns)
            {
                dataRow[column] = dict[column] ?? DBNull.Value;
            }

            dataTable.Rows.Add(dataRow);
        }

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, null);
        bulkCopy.DestinationTableName = $"[{table}]";
        bulkCopy.BatchSize = 1000;
        bulkCopy.BulkCopyTimeout = 120;

        foreach (var column in columns)
        {
            bulkCopy.ColumnMappings.Add(column, column);
        }

        await bulkCopy.WriteToServerAsync(dataTable);
    }
}

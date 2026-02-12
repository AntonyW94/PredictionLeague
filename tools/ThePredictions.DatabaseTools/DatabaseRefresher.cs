using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ThePredictions.DatabaseTools;

public class DatabaseRefresher(
    string sourceConnectionString,
    string targetConnectionString,
    string? testPassword,
    bool anonymise)
{
    private static readonly string[] TablesToSkip =
    [
        "AspNetUserTokens",
        "RefreshTokens",
        "PasswordResetTokens"
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
        "Seasons",
        "Rounds",
        "Matches",
        "BoostDefinitions",
        "Leagues",
        "LeagueMembers",
        "LeagueMemberStats",
        "LeagueBoostRules",
        "LeagueBoostWindows",
        "LeaguePrizeSettings",
        "UserPredictions",
        "RoundResults",
        "LeagueRoundResults",
        "UserBoostUsages",
        "Winnings"
    ];

    private static readonly string[] AllTables = TableCopyOrder
        .Concat(TablesToSkip)
        .ToArray();

    public async Task RunAsync()
    {
        Console.WriteLine("[INFO] Starting database refresh...");
        Console.WriteLine($"[INFO] Anonymisation: {(anonymise ? "enabled" : "disabled")}");

        var tableData = new Dictionary<string, IEnumerable<dynamic>>();

        await using var sourceConnection = new SqlConnection(sourceConnectionString);
        await sourceConnection.OpenAsync();

        foreach (var table in TableCopyOrder)
        {
            Console.WriteLine($"[INFO] Reading [{table}] from source...");

            if (table == "AspNetUserLogins" && anonymise)
            {
                var preservedUserId = await GetPreservedUserIdAsync(sourceConnection);

                if (preservedUserId is not null)
                {
                    var rows = (await sourceConnection.QueryAsync(
                        $"""
                        SELECT
                            *
                        FROM
                            [{table}] l
                        WHERE
                            l.[UserId] = @UserId
                        """,
                        new { UserId = preservedUserId })).ToList();

                    tableData[table] = rows;
                    Console.WriteLine($"[INFO] Read {rows.Count} rows from [{table}] (preserved account only)");
                }
                else
                {
                    tableData[table] = [];
                    Console.WriteLine($"[INFO] Preserved account not found, skipping [{table}]");
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
            tableData["AspNetUsers"] = DataAnonymiser.AnonymiseUsers(tableData["AspNetUsers"]);
            tableData["Leagues"] = DataAnonymiser.AnonymiseLeagues(tableData["Leagues"]);
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

    private static async Task<string?> GetPreservedUserIdAsync(SqlConnection connection)
    {
        return await connection.QueryFirstOrDefaultAsync<string>(
            """
            SELECT
                u.[Id]
            FROM
                [AspNetUsers] u
            WHERE
                u.[Email] = @Email
            """,
            new { Email = DataAnonymiser.PreservedEmail });
    }

    private static async Task DisableForeignKeyConstraintsAsync(SqlConnection connection)
    {
        foreach (var table in AllTables)
        {
            await connection.ExecuteAsync($"ALTER TABLE [{table}] NOCHECK CONSTRAINT ALL");
        }
    }

    private static async Task EnableForeignKeyConstraintsAsync(SqlConnection connection)
    {
        foreach (var table in AllTables.Reverse())
        {
            await connection.ExecuteAsync($"ALTER TABLE [{table}] WITH CHECK CHECK CONSTRAINT ALL");
        }
    }

    private static async Task TruncateAllTablesAsync(SqlConnection connection)
    {
        foreach (var table in AllTables.Reverse())
        {
            await connection.ExecuteAsync($"DELETE FROM [{table}]");
        }
    }

    private static async Task InsertRowsAsync(SqlConnection connection, string table, List<dynamic> rows)
    {
        if (rows.Count == 0)
            return;

        var firstRow = (IDictionary<string, object?>)rows[0];
        var columns = firstRow.Keys.ToList();

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

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, null)
        {
            DestinationTableName = $"[{table}]",
            BatchSize = 1000,
            BulkCopyTimeout = 120
        };

        foreach (var column in columns)
        {
            bulkCopy.ColumnMappings.Add(column, column);
        }

        await bulkCopy.WriteToServerAsync(dataTable);
    }
}

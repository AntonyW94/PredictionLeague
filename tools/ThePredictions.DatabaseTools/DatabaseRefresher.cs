using Dapper;
using Microsoft.Data.SqlClient;

namespace ThePredictions.DatabaseTools;

public class DatabaseRefresher
{
    private readonly string _sourceConnectionString;
    private readonly string _targetConnectionString;
    private readonly string? _testPassword;
    private readonly bool _anonymise;

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

    public DatabaseRefresher(
        string sourceConnectionString,
        string targetConnectionString,
        string? testPassword,
        bool anonymise)
    {
        _sourceConnectionString = sourceConnectionString;
        _targetConnectionString = targetConnectionString;
        _testPassword = testPassword;
        _anonymise = anonymise;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("[INFO] Starting database refresh...");
        Console.WriteLine($"[INFO] Anonymisation: {(_anonymise ? "enabled" : "disabled")}");

        var tableData = new Dictionary<string, IEnumerable<dynamic>>();

        await using var sourceConnection = new SqlConnection(_sourceConnectionString);
        await sourceConnection.OpenAsync();

        foreach (var table in TableCopyOrder)
        {
            Console.WriteLine($"[INFO] Reading [{table}] from source...");

            if (table == "AspNetUserLogins" && _anonymise)
            {
                var preservedUserId = await GetPreservedUserIdAsync(sourceConnection);

                if (preservedUserId is not null)
                {
                    var rows = await sourceConnection.QueryAsync(
                        $"""
                        SELECT
                            *
                        FROM
                            [{table}] l
                        WHERE
                            l.[UserId] = @UserId
                        """,
                        new { UserId = preservedUserId });

                    tableData[table] = rows;
                    Console.WriteLine($"[INFO] Read {rows.Count()} rows from [{table}] (preserved account only)");
                }
                else
                {
                    tableData[table] = [];
                    Console.WriteLine($"[INFO] Preserved account not found, skipping [{table}]");
                }
            }
            else
            {
                var rows = await sourceConnection.QueryAsync($"SELECT * FROM [{table}]");
                tableData[table] = rows;
                Console.WriteLine($"[INFO] Read {rows.Count()} rows from [{table}]");
            }
        }

        if (_anonymise)
        {
            Console.WriteLine("[INFO] Anonymising personal data...");
            var anonymiser = new DataAnonymiser();
            tableData["AspNetUsers"] = anonymiser.AnonymiseUsers(tableData["AspNetUsers"]);
            tableData["Leagues"] = anonymiser.AnonymiseLeagues(tableData["Leagues"]);
        }

        await using var targetConnection = new SqlConnection(_targetConnectionString);
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

            if (_anonymise && _testPassword is not null)
            {
                Console.WriteLine("[INFO] Creating test accounts...");
                var testAccountCreator = new TestAccountCreator(targetConnection, _testPassword);
                await testAccountCreator.CreateTestAccountsAsync();
            }
        }
        finally
        {
            Console.WriteLine("[INFO] Re-enabling foreign key constraints on target...");
            await EnableForeignKeyConstraintsAsync(targetConnection);
        }

        if (_anonymise)
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

        var hasIdentity = await HasIdentityColumnAsync(connection, table);

        var columnList = string.Join(",\n    ", columns.Select(c => $"[{c}]"));
        var paramList = string.Join(",\n    ", columns.Select(c => $"@{c}"));

        var sql = $"""
            INSERT INTO [{table}] (
                {columnList}
            )
            VALUES (
                {paramList}
            )
            """;

        if (hasIdentity)
        {
            sql = $"SET IDENTITY_INSERT [{table}] ON;\n{sql};\nSET IDENTITY_INSERT [{table}] OFF;";
        }

        const int batchSize = 500;

        for (var i = 0; i < rows.Count; i += batchSize)
        {
            var batch = rows.Skip(i).Take(batchSize);

            foreach (var row in batch)
            {
                var parameters = new DynamicParameters();

                foreach (var column in columns)
                {
                    var dict = (IDictionary<string, object?>)row;
                    parameters.Add(column, dict[column]);
                }

                await connection.ExecuteAsync(sql, parameters);
            }
        }
    }

    private static async Task<bool> HasIdentityColumnAsync(SqlConnection connection, string table)
    {
        var result = await connection.QueryFirstOrDefaultAsync<int>(
            """
            SELECT
                COUNT(*)
            FROM
                sys.identity_columns ic
            JOIN sys.tables t
                ON ic.[object_id] = t.[object_id]
            WHERE
                t.[name] = @TableName
            """,
            new { TableName = table });

        return result > 0;
    }
}

using ThePredictions.DatabaseTools;

try
{
    if (args.Length == 0)
        throw new InvalidOperationException("Mode required. Valid modes: " + string.Join(", ", Enum.GetNames<ToolMode>()));

    if (!Enum.TryParse<ToolMode>(args[0], ignoreCase: false, out var mode))
        throw new InvalidOperationException($"Unknown mode: '{args[0]}'. Valid modes: " + string.Join(", ", Enum.GetNames<ToolMode>()));

    switch (mode)
    {
        case ToolMode.DevelopmentRefresh:
        {
            var prodConnectionString = GetRequiredEnvironmentVariable("PROD_CONNECTION_STRING");
            var devConnectionString = GetRequiredEnvironmentVariable("DEV_CONNECTION_STRING");
            var testPassword = GetRequiredEnvironmentVariable("TEST_ACCOUNT_PASSWORD");
            var keepLeagueNames = GetBooleanEnvironmentVariable("KEEP_LEAGUE_NAMES", defaultValue: true);
            var preserveFirstNames = GetBooleanEnvironmentVariable("PRESERVE_FIRST_NAMES", defaultValue: false);
            var refresher = new DatabaseRefresher(
                prodConnectionString,
                devConnectionString,
                testPassword,
                anonymise: true,
                keepLeagueNames: keepLeagueNames,
                preserveFirstNames: preserveFirstNames);
            await refresher.RunAsync();
            break;
        }

        case ToolMode.ProductionBackup:
        {
            var prodConnectionString = GetRequiredEnvironmentVariable("PROD_CONNECTION_STRING");
            var backupConnectionString = GetRequiredEnvironmentVariable("BACKUP_CONNECTION_STRING");
            var backupRefresher = new DatabaseRefresher(prodConnectionString, backupConnectionString, testPassword: null, anonymise: false);
            await backupRefresher.RunAsync();
            break;
        }

        case ToolMode.Migrate:
        {
            var migrateConnectionString = GetRequiredEnvironmentVariable("MIGRATE_CONNECTION_STRING");
            var migrator = new DatabaseMigrator(migrateConnectionString);
            if (!migrator.Run())
                return 1;
            break;
        }

        default:
            throw new ArgumentOutOfRangeException();
    }

    Console.WriteLine("[SUCCESS] Operation completed successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] {ex.Message}");
    return 1;
}

static string GetRequiredEnvironmentVariable(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{name} environment variable is not set or is empty.") : value;
}

static bool GetBooleanEnvironmentVariable(string name, bool defaultValue)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value) || !bool.TryParse(value, out var parsed) ? defaultValue : parsed;
}

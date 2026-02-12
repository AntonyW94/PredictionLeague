using ThePredictions.DatabaseTools;

try
{
    if (args.Length == 0)
        throw new InvalidOperationException("Mode required. Valid modes: " + string.Join(", ", Enum.GetNames<ToolMode>()));

    if (!Enum.TryParse<ToolMode>(args[0], ignoreCase: false, out var mode))
        throw new InvalidOperationException($"Unknown mode: '{args[0]}'. Valid modes: " + string.Join(", ", Enum.GetNames<ToolMode>()));

    var prodConnectionString = GetRequiredEnvironmentVariable("PROD_CONNECTION_STRING");

    switch (mode)
    {
        case ToolMode.DevelopmentRefresh:
            var devConnectionString = GetRequiredEnvironmentVariable("DEV_CONNECTION_STRING");
            var testPassword = GetRequiredEnvironmentVariable("TEST_ACCOUNT_PASSWORD");
            var refresher = new DatabaseRefresher(prodConnectionString, devConnectionString, testPassword, anonymise: true);
            await refresher.RunAsync();
            break;

        case ToolMode.ProductionBackup:
            var backupConnectionString = GetRequiredEnvironmentVariable("BACKUP_CONNECTION_STRING");
            var backupRefresher = new DatabaseRefresher(prodConnectionString, backupConnectionString, testPassword: null, anonymise: false);
            await backupRefresher.RunAsync();
            break;

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

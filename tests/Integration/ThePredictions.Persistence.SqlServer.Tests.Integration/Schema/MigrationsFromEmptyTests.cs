using System.Reflection;
using FluentAssertions;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using ThePredictions.Persistence.SqlServer.Migrations;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Schema;

/// <summary>
/// The three real databases were baselined from a schema that already existed, so
/// <c>0001_Baseline.sql</c> has only ever run there as a no-op and nothing has ever checked that the
/// committed migration set can build the schema from nothing. Every run of this suite now does, because
/// the fixture builds its database that way - these tests assert the result rather than trusting that
/// no exception meant success.
/// </summary>
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class MigrationsFromEmptyTests(SqlServerDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MigrationsFolder = "src/ThePredictions.Persistence.SqlServer/Migrations";

    [Fact]
    public async Task Migrations_ShouldAllBeRecordedInTheJournal_WhenAppliedToAnEmptyDatabase()
    {
        // Arrange - the fixture has already applied them; this is the assertion the run was silent about.
        var expected = MigrationScripts.Names();

        // Act
        var journalled = await QueryAsync<string>(
            $"SELECT [ScriptName] FROM [{MigrationScripts.JournalSchema}].[{MigrationScripts.JournalTable}];");

        // Assert
        journalled.Should().BeEquivalentTo(expected,
            "a script that did not run is a schema the application's SQL was never tested against.");
    }

    [Fact]
    public void EmbeddedScripts_ShouldMatchTheCommittedMigrationFiles_SoNoneIsSilentlyLeftOut()
    {
        // Arrange
        var committed = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot(), MigrationsFolder), "*.sql")
            .Select(Path.GetFileName)
            .Select(name => $"ThePredictions.Persistence.SqlServer.Migrations.{name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Act
        var embedded = MigrationScripts.Names();

        // Assert
        embedded.Should().BeEquivalentTo(committed,
            "the tests embed the migration folder by glob; a mismatch means this suite is building a "
            + "different schema from the one that ships.");
    }

    [Fact]
    public void Migrator_ShouldReportNothingPending_WhenEveryScriptHasApplied()
    {
        // Act
        var upgradeRequired = MigrationRunner.IsUpgradeRequired(ConnectionString);

        // Assert
        upgradeRequired.Should().BeFalse(
            "DbUp keys the journal on the script name, so anything still pending here means a name in "
            + "the journal does not match the name of the script that wrote it.");
    }

    [Fact]
    public async Task TestDatabase_ShouldUseProductionsCollation_SoStringComparisonMatches()
    {
        // Act
        var collation = await ScalarAsync<string>("SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));");

        // Assert - the baseline omits every COLLATE clause on purpose (ADR-0013), so the database
        // default decides how the whole schema compares and sorts text.
        collation.Should().Be("SQL_Latin1_General_CP1_CI_AS");
    }

    private static string RepositoryRoot()
    {
        var root = typeof(MigrationsFromEmptyTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(a => a.Key == "RepositoryRoot")
            ?.Value;

        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("The RepositoryRoot assembly metadata is missing; see this project's csproj.");

        return Path.GetFullPath(root);
    }
}

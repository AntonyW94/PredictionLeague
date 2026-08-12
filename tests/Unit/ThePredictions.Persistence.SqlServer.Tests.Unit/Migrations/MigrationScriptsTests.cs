using FluentAssertions;
using ThePredictions.Persistence.SqlServer.Migrations;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Unit.Migrations;

/// <summary>
/// These names are not cosmetic. DbUp journals each script by its manifest resource name, so every string
/// asserted below is a live primary key in <c>dbo.SchemaVersions</c> on production, dev and backup. If a
/// project rename, a folder rename or a moved file changes one, DbUp stops recognising that script as
/// applied and re-runs it - which for <c>0001_Baseline.sql</c> means replaying 1,353 lines of DDL.
///
/// So this is a change-detector test on purpose, which is normally a smell. The justification is that the
/// thing being detected is a schema-history break that is otherwise invisible until a migration runs
/// against a real database, and by then the journal has already been written. A failure here is not "update
/// the expected value" - it is "either put the name back, or rename the journal keys in every database
/// first". The August 2026 persistence split did exactly that, deliberately and with a backup taken.
/// </summary>
public class MigrationScriptsTests
{
    private const string ExpectedPrefix = "ThePredictions.Persistence.SqlServer.Migrations.";

    // Every script committed to date, in application order.
    private static readonly string[] ExpectedNames =
    [
        $"{ExpectedPrefix}0001_Baseline.sql",
        $"{ExpectedPrefix}0002_CreateEmailSettings.sql",
        $"{ExpectedPrefix}0003_CreatePredictionReminderNotifications.sql",
        $"{ExpectedPrefix}0004_CreateUserBadges.sql",
        $"{ExpectedPrefix}0005_DropRoundResultsTotalPoints.sql",
        $"{ExpectedPrefix}0006_LeagueMemberStatsCachedRanks.sql",
        $"{ExpectedPrefix}0007_PointBoostImagesAtWebp.sql",
        $"{ExpectedPrefix}0008_AspNetUsersEmailRequired.sql"
    ];

    [Fact]
    public void Names_ShouldMatchTheJournalKeysInEveryDatabase()
    {
        // Act
        var names = MigrationScripts.Names();

        // Assert - order included, because DbUp applies scripts in the order this returns them.
        names.Should().Equal(ExpectedNames,
            "these are the ScriptName values already recorded in dbo.SchemaVersions. A change here makes "
            + "DbUp re-run migrations it has already applied - see this class's remarks before touching it.");
    }

    [Fact]
    public void Names_ShouldGrowOnlyByAppending_SoAppliedScriptsKeepTheirPosition()
    {
        // Act
        var names = MigrationScripts.Names();

        // Assert - a new migration is always numbered above the last, so the existing seven keep both
        // their names and their positions. This catches a new script inserted with a duplicate or lower
        // number, which would reorder the set and apply it against a schema it was never written for.
        names.Take(ExpectedNames.Length).Should().Equal(ExpectedNames);
        names.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void JournalTable_ShouldBeTheOneEveryDatabaseAlreadyUses()
    {
        // Assert - a different schema or table name would silently start a second journal, and DbUp would
        // find it empty and re-run everything.
        MigrationScripts.JournalSchema.Should().Be("dbo");
        MigrationScripts.JournalTable.Should().Be("SchemaVersions");
    }
}

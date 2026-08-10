using FluentAssertions;
using ThePredictions.Application.Features.Boosts.Queries;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IBoostCatalogueQuery"/> implementation must do.
///
/// Notice what is <b>not</b> asserted: the order. The port promises none, because <c>ORDER BY</c> defers to
/// the database's collation, and the handler sorts in C# instead. A conformance test that pinned an order
/// here would be asserting a guarantee the port does not make, and would fail against an adapter that is
/// perfectly correct.
/// </summary>
public abstract class BoostCatalogueQueryConformanceTests
{
    protected abstract IBoostCatalogueQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryDefinition_WhenSeveralExist()
    {
        // Arrange
        await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");
        await Seed.AddBoostDefinitionAsync("SHIELD", "Shield");
        await Seed.AddBoostDefinitionAsync("TRIPLE", "Triple Points");

        // Act
        var rows = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - set equality, deliberately order-insensitive.
        rows.Select(r => r.Code).Should().BeEquivalentTo(["DOUBLE", "SHIELD", "TRIPLE"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCarryEveryColumn_WhenADefinitionIsFullyPopulated()
    {
        // Arrange - the seeder fills Code, Name, Scope and ImageUrl; the nullable presentation columns are
        // left unset, so this also pins that they arrive as null rather than empty strings.
        await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Points");

        // Act
        var row = (await Query.ExecuteAsync(CancellationToken.None)).Single();

        // Assert
        row.Code.Should().Be("DOUBLE");
        row.Name.Should().Be("Double Points");
        row.Scope.Should().Be("Round");
        row.ImageUrl.Should().Be("/images/boosts/double.webp");
        row.Description.Should().BeNull();
        row.Tooltip.Should().BeNull();
        row.SelectedImageUrl.Should().BeNull();
        row.DisabledImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmpty_WhenNoDefinitionsExist()
    {
        // Act - nothing seeded; the reset between tests leaves the table empty.
        var rows = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        rows.Should().BeEmpty();
    }
}

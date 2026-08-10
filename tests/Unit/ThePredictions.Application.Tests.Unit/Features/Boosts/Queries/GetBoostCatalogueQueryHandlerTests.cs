using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Boosts.Queries;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Boosts.Queries;

/// <summary>
/// This handler was excluded from coverage until its SQL moved to the persistence adapter. What is left is
/// an ordering rule and a mapping, and both are now measured - which is the point of the persistence split
/// rather than a side effect of it.
/// </summary>
public class GetBoostCatalogueQueryHandlerTests
{
    private readonly IBoostCatalogueQuery _catalogueQuery = Substitute.For<IBoostCatalogueQuery>();
    private readonly GetBoostCatalogueQueryHandler _handler;

    public GetBoostCatalogueQueryHandlerTests()
    {
        _handler = new GetBoostCatalogueQueryHandler(_catalogueQuery);
    }

    [Fact]
    public async Task Handle_ShouldOrderAlphabeticallyByName_WhenTheQueryReturnsRowsInAnyOrder()
    {
        // Arrange - the query promises no order, so the handler must impose one.
        Returns(Row("TRIPLE", "Triple Points"), Row("DOUBLE", "Double Points"), Row("SHIELD", "Shield"));

        // Act
        var result = await _handler.Handle(new GetBoostCatalogueQuery(), CancellationToken.None);

        // Assert
        result.Select(r => r.Name).Should().Equal("Double Points", "Shield", "Triple Points");
    }

    [Fact]
    public async Task Handle_ShouldIgnoreCaseWhenOrdering_SoLowercaseNamesDoNotSortAfterEverythingElse()
    {
        // Arrange - an ordinal sort would put "apex" after "Zephyr", because lowercase letters have higher
        // code points. That is the trap this comparer avoids.
        Returns(Row("Z", "Zephyr"), Row("A", "apex"));

        // Act
        var result = await _handler.Handle(new GetBoostCatalogueQuery(), CancellationToken.None);

        // Assert
        result.Select(r => r.Name).Should().Equal("apex", "Zephyr");
    }

    [Fact]
    public async Task Handle_ShouldCarryEveryField_WhenMappingARow()
    {
        // Arrange
        Returns(new BoostCatalogueRow(
            Code: "DOUBLE",
            Name: "Double Points",
            Description: "Doubles the round's points.",
            Tooltip: "Use it wisely",
            Scope: "Round",
            ImageUrl: "/images/boosts/double.webp",
            SelectedImageUrl: "/images/boosts/double-selected.webp",
            DisabledImageUrl: "/images/boosts/double-disabled.webp"));

        // Act
        var item = (await _handler.Handle(new GetBoostCatalogueQuery(), CancellationToken.None)).Single();

        // Assert
        item.Code.Should().Be("DOUBLE");
        item.Name.Should().Be("Double Points");
        item.Description.Should().Be("Doubles the round's points.");
        item.Tooltip.Should().Be("Use it wisely");
        item.Scope.Should().Be("Round");
        item.ImageUrl.Should().Be("/images/boosts/double.webp");
        item.SelectedImageUrl.Should().Be("/images/boosts/double-selected.webp");
        item.DisabledImageUrl.Should().Be("/images/boosts/double-disabled.webp");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoBoostsAreDefined()
    {
        // Arrange
        Returns();

        // Act
        var result = await _handler.Handle(new GetBoostCatalogueQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    private void Returns(params BoostCatalogueRow[] rows) =>
        _catalogueQuery.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(rows);

    private static BoostCatalogueRow Row(string code, string name) =>
        new(code, name, Description: null, Tooltip: null, Scope: "Round",
            ImageUrl: null, SelectedImageUrl: null, DisabledImageUrl: null);
}

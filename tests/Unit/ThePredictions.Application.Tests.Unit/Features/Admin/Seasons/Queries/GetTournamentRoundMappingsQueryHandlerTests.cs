using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Queries;

/// <summary>
/// Which tournament stages each round of a season covers, for the admin screen that edits them.
/// </summary>
/// <remarks>
/// The stages are stored as one pipe-separated string, and reading it back is the mapping's own job. This handler used to carry
/// a second copy of that parse; the copy is gone, so what is worth asserting here is that the screen shows what the mapping
/// says.
/// </remarks>
public class GetTournamentRoundMappingsQueryHandlerTests
{
    private const int SeasonId = 7;

    private readonly ITournamentRoundMappingRepository _repository = Substitute.For<ITournamentRoundMappingRepository>();
    private readonly GetTournamentRoundMappingsQueryHandler _handler;

    public GetTournamentRoundMappingsQueryHandlerTests()
    {
        _handler = new GetTournamentRoundMappingsQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheSeasonHasNoMappings()
    {
        // Arrange
        GivenMappings();

        // Act
        var mappings = await HandleAsync();

        // Assert
        mappings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEachRoundWithItsNameAndMatchCount()
    {
        // Arrange
        GivenMappings(Mapping(roundNumber: 4, "Quarter Finals", "QuarterFinals", expectedMatchCount: 4));

        // Act
        var mapping = (await HandleAsync()).Single();

        // Assert
        mapping.RoundNumber.Should().Be(4);
        mapping.DisplayName.Should().Be("Quarter Finals");
        mapping.ExpectedMatchCount.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ShouldReturnEveryStageARoundCovers()
    {
        // Arrange - a group round covers several stages at once.
        GivenMappings(Mapping(roundNumber: 1, "Group Stage", "Group1|Group2|Group3", expectedMatchCount: 6));

        // Act
        var mapping = (await HandleAsync()).Single();

        // Assert
        mapping.Stages.Should().Equal(TournamentStage.Group1, TournamentStage.Group2, TournamentStage.Group3);
    }

    [Fact]
    public async Task Handle_ShouldReturnNoStages_ForARoundWithNoneRecorded()
    {
        // Arrange - the column allows an empty string, and the screen has to render that rather than fail.
        GivenMappings(Mapping(roundNumber: 1, "To Be Confirmed", string.Empty, expectedMatchCount: 0));

        // Act
        var mapping = (await HandleAsync()).Single();

        // Assert
        mapping.Stages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEveryMappingTheSeasonHas()
    {
        // Arrange
        GivenMappings(
            Mapping(roundNumber: 1, "Group Stage", "Group1", expectedMatchCount: 6),
            Mapping(roundNumber: 2, "Final", "Final", expectedMatchCount: 1));

        // Act
        var mappings = await HandleAsync();

        // Assert
        mappings.Select(mapping => mapping.RoundNumber).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Handle_ShouldAskAboutTheSeasonItWasGiven()
    {
        // Arrange
        GivenMappings();

        // Act
        await HandleAsync();

        // Assert
        await _repository.Received(1).GetBySeasonIdAsync(SeasonId, Arg.Any<CancellationToken>());
    }

    private static TournamentRoundMapping Mapping(int roundNumber, string displayName, string stages, int expectedMatchCount) =>
        new(id: roundNumber, seasonId: SeasonId, roundNumber, displayName, stages, expectedMatchCount);

    private void GivenMappings(params TournamentRoundMapping[] mappings) =>
        _repository.GetBySeasonIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([.. mappings]);

    private Task<List<Contracts.Admin.Seasons.TournamentRoundMappingDto>> HandleAsync() =>
        _handler.Handle(new GetTournamentRoundMappingsQuery(SeasonId), CancellationToken.None);
}

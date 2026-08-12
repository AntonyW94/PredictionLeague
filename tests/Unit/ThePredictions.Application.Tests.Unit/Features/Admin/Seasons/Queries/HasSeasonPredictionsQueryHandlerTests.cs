using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Application.Repositories;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Queries;

/// <summary>
/// Whether anybody has predicted in a season yet. The admin screens use this to decide what may still be changed, so a false
/// negative would let somebody rewrite a season people have already played.
/// </summary>
public class HasSeasonPredictionsQueryHandlerTests
{
    private const int SeasonId = 7;

    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly HasSeasonPredictionsQueryHandler _handler;

    public HasSeasonPredictionsQueryHandlerTests()
    {
        _handler = new HasSeasonPredictionsQueryHandler(_seasonRepository);
    }

    [Fact]
    public async Task Handle_ShouldReportPredictionsExist_WhenSomebodyHasPredicted()
    {
        // Arrange
        _seasonRepository.HasPredictionsAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var hasPredictions = await HandleAsync();

        // Assert
        hasPredictions.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportNoPredictions_WhenNobodyHasPredicted()
    {
        // Arrange
        _seasonRepository.HasPredictionsAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var hasPredictions = await HandleAsync();

        // Assert
        hasPredictions.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldAskAboutTheSeasonItWasGiven()
    {
        // Act
        await HandleAsync();

        // Assert
        await _seasonRepository.Received(1).HasPredictionsAsync(SeasonId, Arg.Any<CancellationToken>());
    }

    private Task<bool> HandleAsync() =>
        _handler.Handle(new HasSeasonPredictionsQuery(SeasonId), CancellationToken.None);
}

using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Teams.Queries;
using ThePredictions.Application.Features.SeasonPasses.Queries;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.SeasonPasses.Queries;

/// <summary>
/// The team badges shown on the season-pass page.
///
/// Reads through the same port as the administrator's team list, which is what makes this handler small: the two used to ask
/// "which teams are in this season" with a statement each, in different shapes.
/// </summary>
public class GetSeasonTeamsQueryHandlerTests
{
    private const int SeasonId = 7;

    private readonly ISeasonTeamsQuery _seasonTeamsQuery = Substitute.For<ISeasonTeamsQuery>();
    private readonly GetSeasonTeamsQueryHandler _handler;

    public GetSeasonTeamsQueryHandlerTests()
    {
        _handler = new GetSeasonTeamsQueryHandler(_seasonTeamsQuery);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheSeasonHasNoFixturesYet()
    {
        // Arrange
        Given();

        // Act
        var teams = await HandleAsync();

        // Assert
        teams.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListTheTeamsAlphabetically()
    {
        // Arrange
        Given(Team(1, "Wolves"), Team(2, "Arsenal"), Team(3, "Chelsea"));

        // Act
        var teams = await HandleAsync();

        // Assert
        teams.Select(team => team.Name).Should().Equal("Arsenal", "Chelsea", "Wolves");
    }

    [Fact]
    public async Task Handle_ShouldReportEachTeamsBadge()
    {
        // Arrange
        Given(Team(1, "Arsenal"));

        // Act
        var team = (await HandleAsync()).Single();

        // Assert
        team.Name.Should().Be("Arsenal");
        team.LogoUrl.Should().Be("arsenal.png");
    }

    [Fact]
    public async Task Handle_ShouldReportATeamWithNoBadge()
    {
        // Arrange
        Given(Team(1, "Arsenal") with { LogoUrl = null });

        // Act
        var team = (await HandleAsync()).Single();

        // Assert - the page shows a placeholder rather than failing.
        team.LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldAskForTheSeasonRequested()
    {
        // Arrange
        Given();

        // Act
        await HandleAsync();

        // Assert
        await _seasonTeamsQuery.Received(1).ExecuteAsync(SeasonId, Arg.Any<CancellationToken>());
    }

    private void Given(params TeamRow[] teams) =>
        _seasonTeamsQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(teams);

    private static TeamRow Team(int id, string name) =>
        new(id, name, name, $"{name.ToLowerInvariant()}.png", name[..3].ToUpperInvariant(), ApiTeamId: null);

    private Task<IEnumerable<Contracts.SeasonPasses.SeasonTeamDto>> HandleAsync() =>
        _handler.Handle(new GetSeasonTeamsQuery(SeasonId), CancellationToken.None);
}

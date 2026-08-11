using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The tournament stages a league's leaderboard can be filtered by.
/// </summary>
public class GetStagesForLeagueQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private readonly ILeagueSeasonRoundsQuery _seasonRoundsQuery = Substitute.For<ILeagueSeasonRoundsQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetStagesForLeagueQueryHandler _handler;

    public GetStagesForLeagueQueryHandlerTests()
    {
        _handler = new GetStagesForLeagueQueryHandler(_seasonRoundsQuery, _membershipService);
    }

    [Fact]
    public async Task Handle_ShouldCheckMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _seasonRoundsQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldOfferNoStages_ForASeasonWithNoTournamentStructure()
    {
        // Arrange - rounds exist but none is mapped to a stage.
        Given(Round(1, stages: null), Round(2, stages: null));

        // Act
        var stages = await HandleAsync();

        // Assert
        stages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOfferBothStagesInThePlayingOrder()
    {
        // Arrange - the knockout rounds come first in the list, and later in the tournament.
        Given(
            Round(6, stages: "SemiFinals|Final"),
            Round(1, stages: "GroupStage|Group A"),
            Round(2, stages: "GroupStage|Group B"));

        // Act
        var stages = (await HandleAsync()).ToList();

        // Assert
        stages.Select(stage => stage.Stage)
            .Should().Equal(TournamentStageGroup.GroupStage, TournamentStageGroup.KnockoutStage);
    }

    [Fact]
    public async Task Handle_ShouldNameEachStage()
    {
        // Arrange
        Given(Round(1, stages: "GroupStage"), Round(2, stages: "Final"));

        // Act
        var stages = (await HandleAsync()).ToList();

        // Assert - one spelling, shared with the dashboard tile.
        stages.Select(stage => stage.Name).Should().Equal("Group Stage", "Knockout Stage");
    }

    [Fact]
    public async Task Handle_ShouldCountRoundsRemainingAndCompletedWithinAStage()
    {
        // Arrange
        Given(
            Round(1, stages: "GroupStage", status: RoundStatus.Completed),
            Round(2, stages: "GroupStage", status: RoundStatus.Completed),
            Round(3, stages: "GroupStage", status: RoundStatus.InProgress));

        // Act
        var stage = (await HandleAsync()).Single();

        // Assert
        stage.RoundsCompleted.Should().Be(2);
        stage.RoundsRemaining.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotOfferAStageOfNothingButDrafts()
    {
        // Arrange
        Given(
            Round(1, stages: "GroupStage", status: RoundStatus.Published),
            Round(6, stages: "Final", status: RoundStatus.Draft));

        // Act
        var stages = (await HandleAsync()).ToList();

        // Assert
        stages.Select(stage => stage.Stage).Should().Equal(TournamentStageGroup.GroupStage);
    }

    [Fact]
    public async Task Handle_ShouldTreatAMappedRoundThatIsNotAGroupRoundAsKnockout()
    {
        // Arrange
        Given(Round(1, stages: "QuarterFinals"));

        // Act
        var stage = (await HandleAsync()).Single();

        // Assert
        stage.Stage.Should().Be(TournamentStageGroup.KnockoutStage);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreUnmappedRounds_WhenCountingAStagesProgress()
    {
        // Arrange - a friendly with no stage mapping sits between two group rounds.
        Given(
            Round(1, stages: "GroupStage", status: RoundStatus.Completed),
            Round(2, stages: null, status: RoundStatus.Completed),
            Round(3, stages: "GroupStage", status: RoundStatus.Published));

        // Act
        var stage = (await HandleAsync()).Single();

        // Assert
        stage.RoundsCompleted.Should().Be(1);
        stage.RoundsRemaining.Should().Be(1);
    }

    private void Given(params LeagueSeasonRoundRow[] rounds)
    {
        _seasonRoundsQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(rounds);
    }

    private async Task<IEnumerable<StageDto>> HandleAsync() =>
        await _handler.Handle(new GetStagesForLeagueQuery(LeagueId, UserId), CancellationToken.None);

    private static LeagueSeasonRoundRow Round(
        int roundNumber,
        string? stages,
        RoundStatus status = RoundStatus.Published) =>
        new(
            roundNumber,
            roundNumber,
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            status,
            stages);
}

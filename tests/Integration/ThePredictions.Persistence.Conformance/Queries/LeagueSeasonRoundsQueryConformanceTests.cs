using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueSeasonRoundsQuery"/> implementation must return.
///
/// One read serving both league pickers, so it must not do either one's work: no grouping, no counting, no filtering
/// by status, and the stage text raw rather than classified.
/// </summary>
public abstract class LeagueSeasonRoundsQueryConformanceTests
{
    protected abstract ILeagueSeasonRoundsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForALeagueThatDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId + 5_000, CancellationToken.None);

        // Assert
        rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundOfTheSeason_DraftsIncluded()
    {
        // Arrange
        var world = await ArrangeAsync();
        var draftId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(7), RoundStatus.Draft);
        var publishedId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(14));
        var completedId = await Seed.AddRoundAsync(world.SeasonId, 3, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - whether a period made only of drafts is worth offering is a rule, so the drafts have to arrive.
        rounds.Select(round => round.RoundId).Should().BeEquivalentTo([draftId, publishedId, completedId]);
        rounds.Single(round => round.RoundId == draftId).Status.Should().Be(RoundStatus.Draft);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundsNumberAndStartDate()
    {
        // Arrange
        var world = await ArrangeAsync();
        var startDateUtc = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
        await Seed.AddRoundAsync(world.SeasonId, 6, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed, startDateUtc);

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the month picker groups by the start date, the stage picker orders by the round number.
        var round = rounds.Single();
        round.RoundNumber.Should().Be(6);
        round.StartDateUtc.Should().BeCloseTo(startDateUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheStageTextRaw()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1));
        await Seed.AddTournamentRoundMappingAsync(world.SeasonId, 1, "GroupStage|Group A");

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - classifying it is the handler's rule, and doing it here would put a collation dependency back.
        rounds.Single().Stages.Should().Be("GroupStage|Group A");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoStageText_ForARoundWithNoMapping()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1));

        // Assert - null rather than absent: an unmapped round still counts towards its month.
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        rounds.Single().Stages.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAnUnmappedRoundAlongsideMappedOnes()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2));
        await Seed.AddTournamentRoundMappingAsync(world.SeasonId, 1, "GroupStage");
        await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1));

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the stage picker drops the unmapped one, the month picker keeps it. Both need it returned.
        rounds.Should().HaveCount(2);
        rounds.Count(round => round.Stages is null).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoRounds_FromAnotherSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(world.CompetitionId, "2027/28");
        await Seed.AddRoundAsync(otherSeasonId, 1, DateTime.UtcNow.AddDays(-1));

        // Act
        var rounds = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        rounds.Should().BeEmpty();
    }

    private async Task<PickerWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new PickerWorld(leagueId, backdrop.CompetitionId, backdrop.SeasonId);
    }

    private sealed record PickerWorld(int LeagueId, int CompetitionId, int SeasonId);
}

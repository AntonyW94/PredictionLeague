using FluentAssertions;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Repositories;

/// <summary>
/// The read and the write behind a league's round points, which used to be one <c>MERGE</c> holding the scoring formula.
///
/// The arithmetic is <see cref="LeagueScoring"/> and is unit tested. What is left for an adapter is which (league, player)
/// pairs are in scope - every league running the round's season, and only members it has approved - and an upsert. None of
/// it had a test of any kind before, because the statement did both and neither half could be reached without a database.
/// </summary>
public abstract class LeagueRepositoryScoringConformanceTests
{
    protected abstract ILeagueRepository Repository { get; }

    protected abstract ITestDataSeeder Seed { get; }

    /// <summary>Direct reads, bypassing the repository.</summary>
    protected abstract ITestDataInspector Inspect { get; }

    #region Which pairs are in scope

    [Fact]
    public async Task GetLeagueRoundScoringInputsAsync_ShouldReturnAMembersTallyWithTheLeaguesRates()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundResultAsync(world.RoundId, world.UserId, exactScoreCount: 2, correctResultCount: 3, incorrectCount: 4);

        // Act
        var inputs = (await Repository.GetLeagueRoundScoringInputsAsync(world.RoundId, CancellationToken.None)).ToList();

        // Assert - the counts and the rates, not a total: turning them into points is a rule.
        var input = inputs.Single();
        input.LeagueId.Should().Be(world.LeagueId);
        input.UserId.Should().Be(world.UserId);
        input.Counts.ExactScoreCount.Should().Be(2);
        input.Counts.CorrectResultCount.Should().Be(3);
        input.PointsForExactScore.Should().Be(3);
        input.PointsForCorrectResult.Should().Be(1);
    }

    [Fact]
    public async Task GetLeagueRoundScoringInputsAsync_ShouldReturnOnePairPerLeagueThePlayerIsIn()
    {
        // A player in two leagues is scored twice, because each league sets its own rates.
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        await Seed.AddLeagueMemberAsync(otherLeagueId, world.UserId);
        await Seed.AddRoundResultAsync(world.RoundId, world.UserId, exactScoreCount: 1);

        // Act
        var inputs = (await Repository.GetLeagueRoundScoringInputsAsync(world.RoundId, CancellationToken.None)).ToList();

        // Assert
        inputs.Select(input => input.LeagueId).Should().BeEquivalentTo([world.LeagueId, otherLeagueId]);
    }

    [Fact]
    public async Task GetLeagueRoundScoringInputsAsync_ShouldNotScoreSomebodyWhoseRequestToJoinIsPending()
    {
        // Arrange - they have a tally, because the tally is league-agnostic. They are not in this league yet.
        var world = await ArrangeAsync();
        var pendingId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);
        await Seed.AddRoundResultAsync(world.RoundId, pendingId, exactScoreCount: 5);

        // Act
        var inputs = (await Repository.GetLeagueRoundScoringInputsAsync(world.RoundId, CancellationToken.None)).ToList();

        // Assert
        inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLeagueRoundScoringInputsAsync_ShouldNotScoreALeagueFromAnotherSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(world.CompetitionId, "2025/26", isActive: false);
        var otherLeagueId = await Seed.AddLeagueAsync(otherSeasonId, world.UserId, "Last Season");
        await Seed.AddLeagueMemberAsync(otherLeagueId, world.UserId);
        await Seed.AddRoundResultAsync(world.RoundId, world.UserId, exactScoreCount: 1);

        // Act
        var inputs = (await Repository.GetLeagueRoundScoringInputsAsync(world.RoundId, CancellationToken.None)).ToList();

        // Assert
        inputs.Select(input => input.LeagueId).Should().Equal(world.LeagueId);
    }

    [Fact]
    public async Task GetLeagueRoundScoringInputsAsync_ShouldReturnNothing_WhenNobodyHasATallyForTheRound()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var inputs = await Repository.GetLeagueRoundScoringInputsAsync(world.RoundId, CancellationToken.None);

        // Assert
        inputs.Should().BeEmpty();
    }

    #endregion

    #region Storing the points

    [Fact]
    public async Task UpdateLeagueRoundResultsAsync_ShouldStoreWhatItIsGiven()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        await Repository.UpdateLeagueRoundResultsAsync(
            world.RoundId,
            [new LeagueRoundScore(world.LeagueId, world.UserId, BasePoints: 9, BoostedPoints: 9, HasBoost: false, AppliedBoostCode: null)],
            CancellationToken.None);

        // Assert - read back through the scoring inputs of a second round is not possible, so this asserts through the
        // repository's own read of stored results.
        var stored = (await Repository.GetLeagueRoundResultsAsync(world.RoundId, CancellationToken.None)).ToList();
        var result = stored.Single();
        result.LeagueId.Should().Be(world.LeagueId);
        result.UserId.Should().Be(world.UserId);
        result.BasePoints.Should().Be(9);
        result.BoostedPoints.Should().Be(9);
        result.HasBoost.Should().BeFalse();
        result.AppliedBoostCode.Should().BeNull();
    }

    [Fact]
    public async Task UpdateLeagueRoundResultsAsync_ShouldClearABoostAlreadyStored()
    {
        // The reason the caller sends the cleared values rather than leaving them alone: a round re-processed after a score
        // correction must drop the boost so the step that applies boosts can put it back once.
        var world = await ArrangeAsync();
        await Seed.AddLeagueRoundResultAsync(
            world.LeagueId, world.RoundId, world.UserId, basePoints: 9, boostedPoints: 18, appliedBoostCode: "DOUBLE");

        // Act
        await Repository.UpdateLeagueRoundResultsAsync(
            world.RoundId,
            [new LeagueRoundScore(world.LeagueId, world.UserId, BasePoints: 12, BoostedPoints: 12, HasBoost: false, AppliedBoostCode: null)],
            CancellationToken.None);

        // Assert
        var result = (await Repository.GetLeagueRoundResultsAsync(world.RoundId, CancellationToken.None)).Single();
        result.BasePoints.Should().Be(12);
        result.BoostedPoints.Should().Be(12);
        result.HasBoost.Should().BeFalse();
        result.AppliedBoostCode.Should().BeNull();
    }

    [Fact]
    public async Task UpdateLeagueRoundResultsAsync_ShouldLeaveAPlayerAbsentFromTheBatchAlone()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, otherUserId);
        await Seed.AddLeagueRoundResultAsync(
            world.LeagueId, world.RoundId, otherUserId, basePoints: 6, boostedPoints: 6, appliedBoostCode: null!);

        // Act
        await Repository.UpdateLeagueRoundResultsAsync(
            world.RoundId,
            [new LeagueRoundScore(world.LeagueId, world.UserId, BasePoints: 9, BoostedPoints: 9, HasBoost: false, AppliedBoostCode: null)],
            CancellationToken.None);

        // Assert - the other player's points survive, which is what a round re-processed with a reverted fixture relies on.
        var stored = (await Repository.GetLeagueRoundResultsAsync(world.RoundId, CancellationToken.None)).ToList();
        stored.Should().HaveCount(2);
        stored.Single(result => result.UserId == otherUserId).BasePoints.Should().Be(6);
    }

    [Fact]
    public async Task UpdateLeagueRoundResultsAsync_ShouldStoreNothing_WhenThereIsNothingToStore()
    {
        // Arrange - Dapper throws on an empty parameter list, so this has to be handled rather than sent.
        var world = await ArrangeAsync();

        // Act
        var act = async () => await Repository.UpdateLeagueRoundResultsAsync(world.RoundId, [], CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await Repository.GetLeagueRoundResultsAsync(world.RoundId, CancellationToken.None)).Should().BeEmpty();
    }

    #endregion

    #region Applying boosts over the top

    [Fact]
    public async Task UpdateLeagueRoundBoostsAsync_ShouldRaiseTheStoredPointsAndRecordWhichBoostDidIt()
    {
        // Arrange - a scored round, boost cleared, exactly as the scoring pass leaves it.
        var world = await ArrangeAsync();
        await Repository.UpdateLeagueRoundResultsAsync(
            world.RoundId,
            [new LeagueRoundScore(world.LeagueId, world.UserId, BasePoints: 9, BoostedPoints: 9, HasBoost: false, AppliedBoostCode: null)],
            CancellationToken.None);

        // Act
        await Repository.UpdateLeagueRoundBoostsAsync(
            [new LeagueRoundBoostUpdate(world.LeagueId, world.RoundId, world.UserId, BoostedPoints: 18, HasBoost: true, AppliedBoostCode: "DOUBLE")],
            CancellationToken.None);

        // Assert - base points untouched, so re-processing the round can start from them again.
        var stored = await Inspect.LeagueRoundResultAsync(world.LeagueId, world.RoundId, world.UserId);
        stored.Should().NotBeNull();
        stored!.BasePoints.Should().Be(9);
        stored.BoostedPoints.Should().Be(18);
        stored.HasBoost.Should().BeTrue();
        stored.AppliedBoostCode.Should().Be("DOUBLE");
    }

    [Fact]
    public async Task UpdateLeagueRoundBoostsAsync_ShouldUpdateEveryRowInTheBatch()
    {
        // The whole point of the set-based rewrite: one statement, every row. A source that joined on the wrong
        // column, or a JSON path naming a property the rows do not carry, would update one and miss the rest.
        var world = await ArrangeAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, otherUserId);

        await Repository.UpdateLeagueRoundResultsAsync(
            world.RoundId,
            [
                new LeagueRoundScore(world.LeagueId, world.UserId, BasePoints: 9, BoostedPoints: 9, HasBoost: false, AppliedBoostCode: null),
                new LeagueRoundScore(world.LeagueId, otherUserId, BasePoints: 4, BoostedPoints: 4, HasBoost: false, AppliedBoostCode: null)
            ],
            CancellationToken.None);

        // Act
        await Repository.UpdateLeagueRoundBoostsAsync(
            [
                new LeagueRoundBoostUpdate(world.LeagueId, world.RoundId, world.UserId, BoostedPoints: 18, HasBoost: true, AppliedBoostCode: "DOUBLE"),
                new LeagueRoundBoostUpdate(world.LeagueId, world.RoundId, otherUserId, BoostedPoints: 12, HasBoost: true, AppliedBoostCode: "TRIPLE")
            ],
            CancellationToken.None);

        // Assert
        (await Inspect.LeagueRoundResultAsync(world.LeagueId, world.RoundId, world.UserId))!.BoostedPoints.Should().Be(18);
        (await Inspect.LeagueRoundResultAsync(world.LeagueId, world.RoundId, otherUserId))!.BoostedPoints.Should().Be(12);
    }

    [Fact]
    public async Task UpdateLeagueRoundBoostsAsync_ShouldCreateNothing_WhenThereIsNoScoredRowToBoost()
    {
        // Matched-only by design: a boost is an adjustment to points that have been worked out, never a way to
        // bring a row into being with no base points behind it.
        var world = await ArrangeAsync();

        // Act
        await Repository.UpdateLeagueRoundBoostsAsync(
            [new LeagueRoundBoostUpdate(world.LeagueId, world.RoundId, world.UserId, BoostedPoints: 18, HasBoost: true, AppliedBoostCode: "DOUBLE")],
            CancellationToken.None);

        // Assert
        (await Inspect.LeagueRoundResultAsync(world.LeagueId, world.RoundId, world.UserId)).Should().BeNull();
    }

    [Fact]
    public async Task UpdateLeagueRoundBoostsAsync_ShouldStoreNothing_WhenThereIsNothingToStore()
    {
        var world = await ArrangeAsync();

        // Act
        var act = async () => await Repository.UpdateLeagueRoundBoostsAsync([], CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await Inspect.LeagueRoundResultAsync(world.LeagueId, world.RoundId, world.UserId)).Should().BeNull();
    }

    #endregion

    /// <summary>A league whose only member is the seeded player, and one round of its season.</summary>
    private async Task<ScoringWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-1));

        return new ScoringWorld(roundId, leagueId, backdrop.SeasonId, backdrop.CompetitionId, backdrop.UserId);
    }

    private sealed record ScoringWorld(int RoundId, int LeagueId, int SeasonId, int CompetitionId, string UserId);
}

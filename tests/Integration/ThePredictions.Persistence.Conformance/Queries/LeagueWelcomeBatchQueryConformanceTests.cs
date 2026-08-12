using FluentAssertions;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueWelcomeBatchQuery"/> implementation must return.
///
/// The window on the entry deadline is the one filter that stays: it is choosing which leagues, and both instants come from the
/// caller's clock. Everything else the batch decides - who has already been welcomed, whether a league's prizes are settled,
/// whether a boost is on offer - comes back unfiltered, because those decide whether a real email reaches a real player.
/// </summary>
public abstract class LeagueWelcomeBatchQueryConformanceTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowStartUtc = NowUtc.AddDays(-7);
    private static readonly DateTime InsideWindowUtc = NowUtc.AddDays(-1);

    protected abstract ILeagueWelcomeBatchQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    #region Which leagues are in the window

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALeagueWhoseEntryClosedInsideTheWindow()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Leagues.Select(league => league.LeagueId).Should().Equal(world.LeagueId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALeagueWhoseEntryClosedExactlyAtTheStartOfTheWindow()
    {
        // Both ends are inclusive, which is what stops a league slipping between two runs of the job.
        await ArrangeAsync(entryDeadlineUtc: WindowStartUtc);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Leagues.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALeagueWhoseEntryClosedExactlyNow()
    {
        // Arrange
        await ArrangeAsync(entryDeadlineUtc: NowUtc);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Leagues.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnALeagueWhoseEntryHasNotClosedYet()
    {
        // Arrange
        await ArrangeAsync(entryDeadlineUtc: NowUtc.AddDays(1));

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnALeagueThatClosedBeforeTheWindow()
    {
        // Arrange - historic leagues are never back-filled, which is the whole reason the window has a start.
        await ArrangeAsync(entryDeadlineUtc: WindowStartUtc.AddDays(-1));

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnALeagueWithNoEntryDeadline()
    {
        // Arrange - a league that never closes to entry has nobody to welcome yet.
        await ArrangeAsync(entryDeadlineUtc: null);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Leagues.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothingAtAll_WhenNoLeagueIsInTheWindow()
    {
        // Arrange
        await ArrangeAsync(entryDeadlineUtc: null);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Recipients.Should().BeEmpty();
        data.AlreadyNotified.Should().BeEmpty();
        data.Schemes.Should().BeEmpty();
        data.Prizes.Should().BeEmpty();
        data.Boosts.Should().BeEmpty();
        data.BoostWindows.Should().BeEmpty();
    }

    #endregion

    #region What the league says

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheLeagueAndSeasonFactsTheEmailQuotes()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await ExecuteAsync();

        // Assert
        var league = data.Leagues.Single();
        league.LeagueName.Should().Be("Integration League");
        league.SeasonName.Should().Be("2026/27");
        league.HasPrizes.Should().BeTrue();
        league.NumberOfRounds.Should().Be(38);
        league.MemberCount.Should().Be(1);
        league.SeasonStartDateUtc.Should().NotBe(default);
        league.SeasonEndDateUtc.Should().NotBe(default);
        league.LeagueId.Should().Be(world.LeagueId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyApprovedMembers()
    {
        // Arrange - the email tells everybody how many people they are up against.
        var world = await ArrangeAsync();
        var pendingId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Leagues.Single().MemberCount.Should().Be(1);
    }

    #endregion

    #region Who is in it

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryApprovedMemberWithTheirContactDetails()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await ExecuteAsync();

        // Assert
        var recipient = data.Recipients.Single();
        recipient.LeagueId.Should().Be(world.LeagueId);
        recipient.UserId.Should().Be(world.UserId);
        recipient.Email.Should().NotBeNullOrWhiteSpace();
        recipient.FirstName.Should().Be("Ada");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnSomebodyWhoseRequestToJoinIsStillPending()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingId, LeagueMemberStatus.Pending);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Recipients.Select(recipient => recipient.UserId).Should().Equal(world.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEverybodyAlreadyWelcomed()
    {
        // Skipping them is the rule, so the read hands back the sent-log rather than applying it.
        var world = await ArrangeAsync();
        await Seed.AddLeagueWelcomeNotificationAsync(world.LeagueId, world.UserId);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Recipients.Should().ContainSingle();
        data.AlreadyNotified.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { LeagueId = world.LeagueId, UserId = world.UserId });
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAWelcomeSentForAnotherLeague()
    {
        // Arrange - the sent-log is scoped to the leagues in this batch.
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        await Seed.AddLeagueWelcomeNotificationAsync(otherLeagueId, world.UserId);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.AlreadyNotified.Should().BeEmpty();
    }

    #endregion

    #region What the league offers

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALeaguesPrizeScheme()
    {
        // A scheme with no prizes worked out from it yet is what holds the whole league back, so both sets come back
        // separately rather than one being joined to the other.
        var world = await ArrangeAsync();
        await Seed.AddLeaguePrizeSchemeAsync(world.LeagueId, world.UserId);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Schemes.Select(scheme => scheme.LeagueId).Should().Equal(world.LeagueId);
        data.Prizes.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachFrozenPrize()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 100m, rank: 2);

        // Act
        var data = await ExecuteAsync();

        // Assert
        var prize = data.Prizes.Single();
        prize.LeagueId.Should().Be(world.LeagueId);
        prize.PrizeType.Should().Be(PrizeType.Overall);
        prize.Rank.Should().Be(2);
        prize.Amount.Should().Be(100m);
        prize.Stage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnABoostEvenWhenTheLeagueHasSwitchedItOff()
    {
        // Whether a boost is worth telling somebody about is a rule, and it also decides which windows are worth showing -
        // which the statements this replaces had to say twice.
        var world = await ArrangeAsync();
        var definitionId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Up");
        await Seed.AddLeagueBoostRuleAsync(world.LeagueId, definitionId, isEnabled: false);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Boosts.Single().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachBoostWithItsWordingAndSeasonCap()
    {
        // Arrange
        var world = await ArrangeAsync();
        var definitionId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Up");
        var ruleId = await Seed.AddLeagueBoostRuleAsync(world.LeagueId, definitionId, totalUsesPerSeason: 3);

        // Act
        var data = await ExecuteAsync();

        // Assert
        var boost = data.Boosts.Single();
        boost.RuleId.Should().Be(ruleId);
        boost.LeagueId.Should().Be(world.LeagueId);
        boost.Name.Should().Be("Double Up");
        boost.TotalUsesPerSeason.Should().Be(3);
        boost.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachBoostWindowAgainstItsRule()
    {
        // Arrange - the rule id is what ties a window to the boost it restricts.
        var world = await ArrangeAsync();
        var definitionId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Up");
        var ruleId = await Seed.AddLeagueBoostRuleAsync(world.LeagueId, definitionId);
        await Seed.AddLeagueBoostWindowAsync(ruleId, 1, 19, 1);

        // Act
        var data = await ExecuteAsync();

        // Assert
        var window = data.BoostWindows.Single();
        window.LeagueBoostRuleId.Should().Be(ruleId);
        window.StartRoundNumber.Should().Be(1);
        window.EndRoundNumber.Should().Be(19);
        window.MaxUsesInWindow.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothingBelongingToALeagueOutsideTheWindow()
    {
        // Arrange - a league that closed months ago must not have its prizes or boosts dragged into this batch.
        var world = await ArrangeAsync();
        var oldLeagueId = await Seed.AddLeagueAsync(
            world.SeasonId, world.UserId, "Last Month", hasPrizes: true,
            entryDeadlineUtc: WindowStartUtc.AddMonths(-1));

        await Seed.AddLeagueMemberAsync(oldLeagueId, world.UserId);
        await Seed.AddLeaguePrizeSettingAsync(oldLeagueId, PrizeType.Overall, 500m);
        var definitionId = await Seed.AddBoostDefinitionAsync("DOUBLE", "Double Up");
        var ruleId = await Seed.AddLeagueBoostRuleAsync(oldLeagueId, definitionId);
        await Seed.AddLeagueBoostWindowAsync(ruleId, 1, 19, 1);

        // Act
        var data = await ExecuteAsync();

        // Assert
        data.Leagues.Select(league => league.LeagueId).Should().Equal(world.LeagueId);
        data.Recipients.Select(recipient => recipient.LeagueId).Should().AllBeEquivalentTo(world.LeagueId);
        data.Prizes.Should().BeEmpty();
        data.Boosts.Should().BeEmpty();
        data.BoostWindows.Should().BeEmpty();
    }

    #endregion

    private Task<LeagueWelcomeBatchData> ExecuteAsync() =>
        Query.ExecuteAsync(WindowStartUtc, NowUtc, CancellationToken.None);

    /// <summary>A league that closed to entry inside the window, which is the arrangement most of these start from.</summary>
    private Task<WelcomeWorld> ArrangeAsync() => ArrangeAsync(InsideWindowUtc);

    /// <remarks>
    /// Two overloads rather than one optional parameter, because null here means "no deadline set" - a state the column allows
    /// and one of these tests is about. A default of <c>?? InsideWindowUtc</c> would have quietly turned that test into a
    /// duplicate of its neighbours.
    /// </remarks>
    private async Task<WelcomeWorld> ArrangeAsync(DateTime? entryDeadlineUtc)
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(
            backdrop.SeasonId, backdrop.UserId, hasPrizes: true, entryDeadlineUtc: entryDeadlineUtc);

        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new WelcomeWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record WelcomeWorld(int LeagueId, int SeasonId, string UserId);
}

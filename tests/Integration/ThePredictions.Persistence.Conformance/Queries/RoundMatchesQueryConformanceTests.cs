using FluentAssertions;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IRoundMatchesQuery"/> implementation must return: every fixture in the round, in no order.
///
/// Two statements did this before, one for the administrator's editor and one for the players' view, and their
/// differences were the point. One filtered out called-off fixtures, one ordered by kick-off, one left out the
/// per-fixture lock time and one declared the joined team columns never-null. Only the first was a rule, and it now
/// belongs to the handler that needs it.
/// </summary>
public abstract class RoundMatchesQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime KickOff = new(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc);

    protected abstract IRoundMatchesQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForARoundWithNoFixtures()
    {
        // Arrange - a round created before its fixtures are loaded, which is a real state rather than an edge case.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        // Act
        var matches = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert - empty, not one row of nulls. The old left join produced the latter.
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForARoundThatDoesNotExist()
    {
        // Act
        var matches = await Query.ExecuteAsync(-1, CancellationToken.None);

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAFixtureWithBothTeamsScoresAndLockTime()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        var matchId = await Seed.AddMatchAsync(
            roundId, backdrop.HomeTeamId, backdrop.AwayTeamId,
            matchDateTimeUtc: KickOff, customLockTimeUtc: KickOff.AddHours(-1), matchNumber: 3);

        // Act
        var match = (await Query.ExecuteAsync(roundId, CancellationToken.None)).Single();

        // Assert
        match.Id.Should().Be(matchId);
        match.MatchDateTimeUtc.Should().Be(KickOff);
        match.MatchNumber.Should().Be(3);
        match.HomeTeamId.Should().Be(backdrop.HomeTeamId);
        match.HomeTeamName.Should().Be("Arsenal");
        match.HomeTeamAbbreviation.Should().Be("ARS");
        match.AwayTeamId.Should().Be(backdrop.AwayTeamId);
        match.AwayTeamName.Should().Be("Chelsea");
        match.AwayTeamAbbreviation.Should().Be("CHE");
        match.Status.Should().Be(MatchStatus.Scheduled);

        // The lock time was missing from the administrator's copy of this statement, so it could never have been shown
        // on the screen that sets it.
        match.CustomLockTimeUtc.Should().Be(KickOff.AddHours(-1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAFixtureWhoseTeamsAreNotKnownYet()
    {
        // Arrange - a tournament fixture scheduled before the teams that will play it are decided.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        await Seed.AddMatchAsync(roundId, homeTeamId: null, awayTeamId: null, matchDateTimeUtc: KickOff);

        // Act
        var match = (await Query.ExecuteAsync(roundId, CancellationToken.None)).Single();

        // Assert - every joined team column is null here, which is exactly why they are nullable. One of the two
        // statements this replaces said they never were.
        match.HomeTeamId.Should().BeNull();
        match.HomeTeamName.Should().BeNull();
        match.HomeTeamShortName.Should().BeNull();
        match.HomeTeamAbbreviation.Should().BeNull();
        match.HomeTeamLogoUrl.Should().BeNull();
        match.AwayTeamName.Should().BeNull();
        match.AwayTeamAbbreviation.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAFixtureThatHasBeenCalledOff()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId, status: MatchStatus.Postponed);

        // Act
        var matches = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert - whether to show it is a rule, and the two screens answer it differently. The read must not decide.
        matches.Single().Status.Should().Be(MatchStatus.Postponed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryFixtureOfTheRoundAndNoOthers()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var otherRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        var firstId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId, matchDateTimeUtc: KickOff);
        var secondId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId, matchDateTimeUtc: KickOff.AddHours(2));
        await Seed.AddMatchAsync(otherRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId, matchDateTimeUtc: KickOff.AddDays(7));

        // Act
        var matches = await Query.ExecuteAsync(roundId, CancellationToken.None);

        // Assert
        matches.Select(match => match.Id).Should().BeEquivalentTo([firstId, secondId]);
    }
}

using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;
using static ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands.SyncSeasonScenario;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// Rewrites a season's fixtures from the live football feed. It has to cope with the feed moving
/// matches between rounds, postponing and reinstating them, and dropping them entirely - while never
/// destroying a fixture someone has already predicted.
/// </summary>
public class SyncSeasonWithApiCommandHandlerTests
{
    private static readonly DateTime Week1 = new(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Week2 = new(2026, 8, 22, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Week3 = new(2026, 8, 29, 15, 0, 0, DateTimeKind.Utc);

    private readonly SyncSeasonScenario _scenario = new();

    private Task HandleAsync() => _scenario.HandleAsync();

    private List<Round> UpdatedRounds()
    {
        var updated = new List<Round>();
        _scenario.Rounds.UpdateAsync(Arg.Do<Round>(updated.Add), Arg.Any<CancellationToken>());
        return updated;
    }

    // ---------- guards ----------

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheSeasonDoesNotExist()
    {
        _scenario.GivenNoSeason();

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheCompetitionDoesNotExist()
    {
        _scenario.GivenNoCompetition();

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheCompetitionIsNotLinkedToTheApi()
    {
        _scenario.GivenCompetition(apiLeagueId: null);

        await HandleAsync();

        await _scenario.FootballData.DidNotReceiveWithAnyArgs().GetAllFixturesForSeasonAsync(default, default, CancellationToken.None);
        await _scenario.Mediator.DidNotReceive().Send(Arg.Any<PublishUpcomingRoundsCommand>(), Arg.Any<CancellationToken>());
    }

    // ---------- filtering what the feed sends ----------

    [Fact]
    public async Task Handle_ShouldIgnoreAFixtureWhoseTeamsAreNotInTheDatabase()
    {
        // Better to skip it than to guess: an unmapped team would otherwise be scored against
        // nobody.
        _scenario.GivenNoTeamsKnown();
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));
        var updated = UpdatedRounds();

        await HandleAsync();

        updated.Should().BeEmpty();
        await _scenario.Rounds.DidNotReceiveWithAnyArgs().CreateAsync(default!, CancellationToken.None);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Handle_ShouldIgnoreAnIncompleteFixturePayload(bool withFixture, bool withTeams, bool withRoundName)
    {
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1),
            withTeams: withTeams, withFixture: withFixture, withRoundName: withRoundName));

        await HandleAsync();

        await _scenario.Rounds.DidNotReceiveWithAnyArgs().CreateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAnApiRoundNameThatCarriesNoNumber()
    {
        _scenario.GivenApiRoundNames("Relegation Play-offs");
        _scenario.GivenApiFixtures(Fixture(5001, Week1, "Relegation Play-offs"));

        await HandleAsync();

        await _scenario.Rounds.DidNotReceiveWithAnyArgs().CreateAsync(default!, CancellationToken.None);
    }

    // ---------- creating rounds ----------

    [Fact]
    public async Task Handle_ShouldCreateAMissingRoundAndAddItsFixtures()
    {
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));
        Round? created = null;
        _scenario.Rounds.CreateAsync(Arg.Do<Round>(r => created = r), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Round>(), 900));

        await HandleAsync();

        created.Should().NotBeNull();
        created!.RoundNumber.Should().Be(1);
        created.ApiRoundName.Should().Be(RoundName(1));
        created.StartDateUtc.Should().Be(Week1);
        created.DeadlineUtc.Should().Be(Week1.AddMinutes(-30));
    }

    [Fact]
    public async Task Handle_ShouldAddANewFixtureToAnExistingRound()
    {
        var round = Round(1, 1, Week1);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));

        await HandleAsync();

        round.Matches.Should().ContainSingle();
        round.Matches.Single().ExternalId.Should().Be(5001);
        await _scenario.Rounds.Received(1).UpdateAsync(round, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLeaveAnUnchangedFixtureAlone()
    {
        var round = Round(1, 1, Week1, matches: Match(1, 1, Week1, externalId: 5001));
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));

        await HandleAsync();

        await _scenario.Rounds.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldUpdateAKickOffTimeThatHasMoved()
    {
        var match = Match(1, 1, Week1, externalId: 5001);
        var round = Round(1, 1, Week1, matches: match);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1.AddHours(2), RoundName(1)));

        await HandleAsync();

        match.MatchDateTimeUtc.Should().Be(Week1.AddHours(2));
        await _scenario.Rounds.Received(1).UpdateAsync(round, Arg.Any<CancellationToken>());
    }

    // ---------- moving a fixture between rounds ----------

    [Fact]
    public async Task Handle_ShouldMoveAFixtureThatHasBeenRescheduledIntoAnotherRound()
    {
        // The feed regularly shifts a match a week either way; it has to follow, not be duplicated.
        var match = Match(1, 1, Week1, externalId: 5001);
        var roundOne = Round(1, 1, Week1, matches: match);
        var roundTwo = Round(2, 2, Week2, matches: Match(2, 2, Week2, externalId: 5002, homeTeamId: 103, awayTeamId: 104));
        _scenario.GivenRounds(roundOne, roundTwo);
        _scenario.GivenApiRoundNames(RoundName(1), RoundName(2));
        _scenario.GivenApiFixtures(
            Fixture(5001, Week2, RoundName(2)),
            Fixture(5002, Week2, RoundName(2), apiHomeTeamId: 3, apiAwayTeamId: 4));

        await HandleAsync();

        roundOne.Matches.Should().NotContain(m => m.ExternalId == 5001);
        roundTwo.Matches.Should().Contain(m => m.ExternalId == 5001);
    }

    [Fact]
    public async Task Handle_ShouldMoveTheMatchInTheDatabaseBeforeSavingAnyRound()
    {
        // The move has to land first, or saving the source round would delete the match it no
        // longer owns.
        var roundOne = Round(1, 1, Week1, matches: Match(1, 1, Week1, externalId: 5001));
        var roundTwo = Round(2, 2, Week2, matches: Match(2, 2, Week2, externalId: 5002, homeTeamId: 103, awayTeamId: 104));
        _scenario.GivenRounds(roundOne, roundTwo);
        _scenario.GivenApiRoundNames(RoundName(1), RoundName(2));
        _scenario.GivenApiFixtures(
            Fixture(5001, Week2, RoundName(2)),
            Fixture(5002, Week2, RoundName(2)));

        var order = new List<string>();
        await _scenario.Rounds.MoveMatchesToRoundAsync(
            Arg.Do<IEnumerable<int>>(ids => order.Add($"move:{string.Join(",", ids)}")), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _scenario.Rounds.UpdateAsync(Arg.Do<Round>(r => order.Add($"save:{r.Id}")), Arg.Any<CancellationToken>());
        order.Clear();

        await HandleAsync();

        order.Should().NotBeEmpty();
        order[0].Should().Be("move:1", "the match must change rounds before either round is saved");
        order.Skip(1).Should().OnlyContain(o => o.StartsWith("save:"));
    }

    // ---------- postponements ----------

    [Fact]
    public async Task Handle_ShouldPostponeAMatchTheFeedHasCalledOff()
    {
        var match = Match(1, 1, Week1, externalId: 5001);
        var round = Round(1, 1, Week1, matches: match);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1), status: "PST"));

        await HandleAsync();

        match.Status.Should().Be(MatchStatus.Postponed);
    }

    [Fact]
    public async Task Handle_ShouldNotPostponeAMatchThatHasAlreadyBeenPlayed()
    {
        // A completed result stands even if the feed later reports the fixture as postponed.
        var match = Match(1, 1, Week1, externalId: 5001, status: MatchStatus.Completed);
        var round = Round(1, 1, Week1, matches: match);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1), status: "PST"));

        await HandleAsync();

        match.Status.Should().Be(MatchStatus.Completed);
    }

    [Fact]
    public async Task Handle_ShouldReinstateAMatchThatIsBackOn()
    {
        var match = Match(1, 1, Week1, externalId: 5001, status: MatchStatus.Postponed);
        var round = Round(1, 1, Week1, matches: match);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));

        await HandleAsync();

        match.Status.Should().Be(MatchStatus.Scheduled);
    }

    // ---------- stale matches ----------

    [Fact]
    public async Task Handle_ShouldRemoveAMatchTheFeedNoLongerLists()
    {
        var stale = Match(9, 1, Week1, externalId: 5999);
        var round = Round(1, 1, Week1, null, RoundStatus.Draft, Match(1, 1, Week1, externalId: 5001), stale);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));

        await HandleAsync();

        round.Matches.Should().NotContain(m => m.Id == 9);
    }

    [Fact]
    public async Task Handle_ShouldKeepAStaleMatchThatPlayersHaveAlreadyPredicted()
    {
        // Deleting it would destroy their predictions and silently change the scoring.
        var stale = Match(9, 1, Week1, externalId: 5999);
        var round = Round(1, 1, Week1, null, RoundStatus.Draft, Match(1, 1, Week1, externalId: 5001), stale);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));
        _scenario.GivenMatchesWithPredictions(9);

        await HandleAsync();

        round.Matches.Should().Contain(m => m.Id == 9);
    }

    [Fact]
    public async Task Handle_ShouldLeaveAManuallyAddedMatchAlone()
    {
        // A match with no external id did not come from the feed, so the feed does not get to
        // remove it.
        var manual = Match(9, 1, Week1, externalId: null);
        var round = Round(1, 1, Week1, null, RoundStatus.Draft, Match(1, 1, Week1, externalId: 5001), manual);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));

        await HandleAsync();

        round.Matches.Should().Contain(m => m.Id == 9);
    }

    // ---------- round dates ----------

    [Fact]
    public async Task Handle_ShouldPullTheRoundForwardToItsEarliestMatch()
    {
        var round = Round(1, 1, Week1.AddDays(1));
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(
            Fixture(5001, Week1, RoundName(1)),
            Fixture(5002, Week1.AddHours(3), RoundName(1), apiHomeTeamId: 3, apiAwayTeamId: 4));

        await HandleAsync();

        round.StartDateUtc.Should().Be(Week1);
        round.DeadlineUtc.Should().Be(Week1.AddMinutes(-30));
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAPostponedMatchWhenSettingTheRoundStart()
    {
        // A called-off match must not drag the deadline back and lock predictions early.
        var postponed = Match(1, 1, Week1.AddDays(-3), externalId: 5001, status: MatchStatus.Postponed);
        var round = Round(1, 1, Week1.AddDays(1), matches: postponed);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(
            Fixture(5001, Week1.AddDays(-3), RoundName(1), status: "PST"),
            Fixture(5002, Week1, RoundName(1), apiHomeTeamId: 3, apiAwayTeamId: 4));

        await HandleAsync();

        round.StartDateUtc.Should().Be(Week1);
    }

    // ---------- allocation by window ----------

    [Fact]
    public async Task Handle_ShouldAllocateAFixtureToWhicheverRoundItIsNearer()
    {
        // Windows sit at the midpoint between neighbouring rounds' median dates, so a match moved
        // a few days lands in the round it is closest to rather than staying put.
        var roundOne = Round(1, 1, Week1);
        var roundTwo = Round(2, 2, Week3);
        _scenario.GivenRounds(roundOne, roundTwo);
        _scenario.GivenApiRoundNames(RoundName(1), RoundName(2));
        _scenario.GivenApiFixtures(
            // Round 1's median sits in week 1; the stray fixture is far closer to round 2's.
            Fixture(5001, Week1, RoundName(1)),
            Fixture(5004, Week1.AddDays(1), RoundName(1), apiHomeTeamId: 7, apiAwayTeamId: 8),
            Fixture(5003, Week3.AddDays(-1), RoundName(1), apiHomeTeamId: 5, apiAwayTeamId: 6),
            Fixture(5002, Week3, RoundName(2), apiHomeTeamId: 3, apiAwayTeamId: 4));

        await HandleAsync();

        roundTwo.Matches.Should().Contain(m => m.ExternalId == 5003,
            "it kicks off nearer round 2's median than round 1's");
        roundOne.Matches.Select(m => m.ExternalId).Should().BeEquivalentTo([5001, 5004]);
    }

    [Fact]
    public async Task Handle_ShouldStillUpdateTheDateOfAFixtureItCannotPlace()
    {
        // Nothing to allocate it to, but the kick-off change is still real and worth recording.
        var match = Match(1, 1, Week1, externalId: 5001);
        var round = Round(1, 1, Week1, matches: match);
        _scenario.GivenRounds(round);
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(
            Fixture(5001, Week1, RoundName(1)),
            Fixture(5002, Week1, "Relegation Play-offs", apiHomeTeamId: 3, apiAwayTeamId: 4));

        await HandleAsync();

        match.MatchDateTimeUtc.Should().Be(Week1);
    }

    // ---------- finishing up ----------

    [Fact]
    public async Task Handle_ShouldRepublishRoundsAfterSyncing()
    {
        _scenario.GivenApiRoundNames(RoundName(1));
        _scenario.GivenApiFixtures(Fixture(5001, Week1, RoundName(1)));

        await HandleAsync();

        await _scenario.Mediator.Received(1).Send(Arg.Any<PublishUpcomingRoundsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSaveOnlyTheRoundsThatActuallyChanged()
    {
        var untouched = Round(2, 2, Week2, matches: Match(2, 2, Week2, externalId: 5002));
        var changed = Round(1, 1, Week1);
        _scenario.GivenRounds(changed, untouched);
        _scenario.GivenApiRoundNames(RoundName(1), RoundName(2));
        _scenario.GivenApiFixtures(
            Fixture(5001, Week1, RoundName(1)),
            Fixture(5002, Week2, RoundName(2)));
        var updated = UpdatedRounds();

        await HandleAsync();

        updated.Should().ContainSingle().Which.Id.Should().Be(1);
    }
}

using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;
using static ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands.SyncSeasonScenario;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// A tournament is synced differently from a league: its rounds and placeholder matches already
/// exist from season setup, and the feed's job is to fill in who is actually playing as each
/// knockout tie is decided.
/// </summary>
public class SyncSeasonWithApiTournamentTests
{
    private const string SemiFinals = "Semi-finals";
    private const string Final = "Final";

    private static readonly DateTime SemiFinalDate = new(2027, 5, 5, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FinalDate = new(2027, 5, 30, 19, 0, 0, DateTimeKind.Utc);

    private readonly SyncSeasonScenario _scenario = new();

    public SyncSeasonWithApiTournamentTests() => _scenario.GivenCompetition(isTournament: true);

    private Task HandleAsync() => _scenario.HandleAsync();

    private static TournamentRoundMapping Mapping(int roundNumber, params TournamentStage[] stages) =>
        WithId(TournamentRoundMapping.Create(SeasonId, roundNumber, $"Round {roundNumber}",
            string.Join("|", stages), stages.Length), roundNumber);

    /// <summary>A placeholder created at season setup: no teams, no external id, tagged with its stage.</summary>
    private static Match Placeholder(int id, int roundId, DateTime dateUtc, string stageDisplayName) =>
        Match(id, roundId, dateUtc, externalId: null, homeTeamId: null, awayTeamId: null,
            apiRoundName: stageDisplayName, placeholderHome: "Winner SF1", placeholderAway: "Winner SF2");

    [Fact]
    public async Task Handle_ShouldFillAPlaceholderOnceTheTieIsKnown()
    {
        var placeholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals));

        await HandleAsync();

        placeholder.ExternalId.Should().Be(6001);
        placeholder.AreTeamsConfirmed.Should().BeTrue();
        placeholder.HomeTeamId.Should().Be(101);
        placeholder.AwayTeamId.Should().Be(102);
        await _scenario.Rounds.Received(1).UpdateAsync(round, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldOnlyFillAPlaceholderTaggedWithTheSameStage()
    {
        // A combined round holds placeholders for several stages; a semi-final must not be written
        // into the final's slot.
        var semiPlaceholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var finalPlaceholder = Placeholder(2, 5, FinalDate, Final);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, semiPlaceholder, finalPlaceholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals, TournamentStage.Final));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals));

        await HandleAsync();

        semiPlaceholder.ExternalId.Should().Be(6001);
        finalPlaceholder.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldUpdateAKickOffThatHasMoved_OnAnAlreadySyncedTie()
    {
        var match = Match(1, 5, SemiFinalDate, externalId: 6001, apiRoundName: SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, match);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate.AddHours(1), SemiFinals));

        await HandleAsync();

        match.MatchDateTimeUtc.Should().Be(SemiFinalDate.AddHours(1));
    }

    [Fact]
    public async Task Handle_ShouldAddAnExtraTie_WhenThereIsNoPlaceholderLeft()
    {
        // The expected match count can be wrong (a replay, or a mis-set mapping); an extra fixture
        // is added rather than dropped on the floor.
        var taken = Match(1, 5, SemiFinalDate, externalId: 6001, apiRoundName: SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, taken);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(
            Fixture(6001, SemiFinalDate, SemiFinals),
            Fixture(6002, SemiFinalDate.AddHours(3), SemiFinals, apiHomeTeamId: 3, apiAwayTeamId: 4));

        await HandleAsync();

        round.Matches.Should().Contain(m => m.ExternalId == 6002);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAFixtureWhoseRoundNameIsNotAKnownStage()
    {
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, Placeholder(1, 5, SemiFinalDate, SemiFinals));
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, "Some Unknown Phase"));

        await HandleAsync();

        round.Matches.Single().ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAStageTheSeasonHasNoMappingFor()
    {
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, Placeholder(1, 5, SemiFinalDate, SemiFinals));
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, FinalDate, Final));

        await HandleAsync();

        round.Matches.Single().ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldSkipAMappingWithNoRoundInTheDatabase()
    {
        _scenario.GivenRounds();
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals));

        var act = () => HandleAsync();

        await act.Should().NotThrowAsync();
        await _scenario.Rounds.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldPullTheRoundForwardToItsEarliestConfirmedTie()
    {
        var placeholder = Placeholder(1, 5, SemiFinalDate.AddDays(2), SemiFinals);
        var round = Round(5, 1, SemiFinalDate.AddDays(2), SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals));

        await HandleAsync();

        round.StartDateUtc.Should().Be(SemiFinalDate);
        round.DeadlineUtc.Should().Be(SemiFinalDate.AddMinutes(-30));
    }

    [Fact]
    public async Task Handle_ShouldIgnoreUnconfirmedPlaceholdersWhenSettingTheRoundStart()
    {
        // A placeholder still carries its provisional date; only decided ties should move the
        // deadline, or predictions would lock against a date nobody is playing on.
        var confirmed = Match(1, 5, FinalDate, externalId: 6001, apiRoundName: SemiFinals);
        var stillUnknown = Placeholder(2, 5, SemiFinalDate.AddYears(-1), SemiFinals);
        var round = Round(5, 1, FinalDate, SemiFinals, RoundStatus.Draft, confirmed, stillUnknown);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, FinalDate, SemiFinals));

        await HandleAsync();

        round.StartDateUtc.Should().Be(FinalDate);
    }

    [Fact]
    public async Task Handle_ShouldPostponeATieTheFeedHasCalledOff()
    {
        var match = Match(1, 5, SemiFinalDate, externalId: 6001, apiRoundName: SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, match);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals, status: "PST"));

        await HandleAsync();

        match.Status.Should().Be(MatchStatus.Postponed);
    }

    [Fact]
    public async Task Handle_ShouldNotPostponeATieThatHasAlreadyBeenPlayed()
    {
        var match = Match(1, 5, SemiFinalDate, externalId: 6001, status: MatchStatus.Completed, apiRoundName: SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, match);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals, status: "PST"));

        await HandleAsync();

        match.Status.Should().Be(MatchStatus.Completed);
    }

    [Fact]
    public async Task Handle_ShouldReinstateATieThatIsBackOn()
    {
        var match = Match(1, 5, SemiFinalDate, externalId: 6001, status: MatchStatus.Postponed, apiRoundName: SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, match);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals));

        await HandleAsync();

        match.Status.Should().Be(MatchStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_ShouldRepublishRoundsAfterSyncing()
    {
        _scenario.GivenRounds();
        _scenario.GivenMappings();
        _scenario.GivenApiFixtures();

        await HandleAsync();

        await _scenario.Mediator.Received(1).Send(Arg.Any<PublishUpcomingRoundsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotAskForLeagueRoundNames()
    {
        // The tournament path works from the mappings, not the feed's round list.
        _scenario.GivenRounds();
        _scenario.GivenMappings();

        await HandleAsync();

        await _scenario.FootballData.DidNotReceiveWithAnyArgs().GetRoundsForSeasonAsync(default, default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAFixtureTheFeedSentWithoutTeams()
    {
        // A tie can appear in the feed before both sides are decided; it carries no teams and must
        // not be counted when working out which teams to look up.
        var placeholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals, withTeams: false));

        await HandleAsync();

        placeholder.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAFixtureTheFeedSentWithNoDetailAtAll()
    {
        var placeholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals, withFixture: false));

        await HandleAsync();

        placeholder.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreATieBetweenTeamsTheSiteDoesNotKnow()
    {
        // The teams table is seeded separately; a tie between two unknown teams is skipped rather
        // than creating a fixture with no sides.
        var placeholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenNoTeamsKnown();
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals));

        await HandleAsync();

        placeholder.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAStatusUpdateForATieTheRoundDoesNotHold()
    {
        // The feed can report a tie that was never synced into this round; there is nothing local
        // to postpone or reinstate, so it is skipped.
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Published,
            Match(1, 5, SemiFinalDate, externalId: 6001, apiRoundName: SemiFinals));
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(
            Fixture(6001, SemiFinalDate, SemiFinals),
            Fixture(6002, SemiFinalDate, SemiFinals, status: "PST", apiHomeTeamId: 3, apiAwayTeamId: 4));

        var act = () => HandleAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ShouldLockTheLaterStageAsOneBatchWhenARoundSpansTwoStages()
    {
        // A round covering both semi-finals and the final locks the final with its own batch, 30
        // minutes before the earliest kick-off in it, rather than at the round deadline.
        var semiPlaceholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var finalPlaceholder = Placeholder(2, 5, FinalDate, Final);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, semiPlaceholder, finalPlaceholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals, TournamentStage.Final));
        _scenario.GivenApiFixtures(
            Fixture(6001, SemiFinalDate, SemiFinals),
            Fixture(6002, FinalDate, Final, apiHomeTeamId: 3, apiAwayTeamId: 4));

        await HandleAsync();

        semiPlaceholder.CustomLockTimeUtc.Should().BeNull();
        finalPlaceholder.CustomLockTimeUtc.Should().Be(FinalDate.AddMinutes(-30));
    }

    [Fact]
    public async Task Handle_ShouldIgnoreATieWithOnlyTheAwaySideNamed()
    {
        // A knockout tie can be published with one side still to be decided.
        var placeholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals, withHomeTeam: false));

        await HandleAsync();

        placeholder.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreATieWhoseLeagueBlockCarriesNoStageName()
    {
        // The feed sends the competition but leaves the stage blank, so there is nothing to match
        // the tie to a round with.
        var placeholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals, withRoundNameValue: false));

        await HandleAsync();

        placeholder.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreATieWithOnlyTheHomeSideNamed()
    {
        // The other half of the draw has not finished, so the away side is still to be decided.
        var placeholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals, withAwayTeam: false));

        await HandleAsync();

        placeholder.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreATieWithNoCompetitionBlockAtAll()
    {
        // Without the competition block there is no stage to place the tie in.
        var placeholder = Placeholder(1, 5, SemiFinalDate, SemiFinals);
        var round = Round(5, 1, SemiFinalDate, SemiFinals, RoundStatus.Draft, placeholder);
        _scenario.GivenRounds(round);
        _scenario.GivenMappings(Mapping(1, TournamentStage.SemiFinals));
        _scenario.GivenApiFixtures(Fixture(6001, SemiFinalDate, SemiFinals, withRoundName: false));

        await HandleAsync();

        placeholder.ExternalId.Should().BeNull();
    }
}

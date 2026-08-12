using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Matches;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

/// <summary>
/// Creating a round by hand, for a competition with no data feed to sync from.
/// </summary>
public class CreateRoundCommandHandlerTests
{
    private const int SeasonId = 11;
    private const int CreatedRoundId = 55;

    private static readonly DateTime StartDateUtc = new(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc);

    private readonly IRoundRepository _rounds = Substitute.For<IRoundRepository>();
    private readonly ISeasonRepository _seasons = Substitute.For<ISeasonRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly CreateRoundCommandHandler _handler;

    public CreateRoundCommandHandlerTests()
    {
        _handler = new CreateRoundCommandHandler(_rounds, _seasons, _currentUser);
        _rounds.CreateAsync(Arg.Any<Round>(), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Round>()));

        GivenSeasonWithRoomForMoreRounds();
    }

    [Fact]
    public async Task Handle_ShouldRefuseARoundBeyondTheNumberTheSeasonDeclares()
    {
        // The declared number divides the prize pot, so a season quietly carrying an extra round pays out the wrong round
        // prizes. Making room is a deliberate act on the season, with that consequence in view.
        GivenSeasonWithRoomForMoreRounds(numberOfRounds: 3, roundsAlreadyHeld: 3);

        // Act
        var act = async () => await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        await _rounds.DidNotReceiveWithAnyArgs().CreateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldAllowTheLastRoundTheSeasonDeclares()
    {
        // Arrange - the boundary: three declared, two held, so the third is allowed.
        GivenSeasonWithRoomForMoreRounds(numberOfRounds: 3, roundsAlreadyHeld: 2);

        // Act
        var round = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        round.Id.Should().Be(CreatedRoundId);
    }

    /// <summary>A season declaring 38 rounds and holding none, so the limit is not what any other test is about.</summary>
    private void GivenSeasonWithRoomForMoreRounds(int numberOfRounds = 38, int roundsAlreadyHeld = 0)
    {
        _seasons.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(Season(numberOfRounds));

        _rounds.GetAllForSeasonAsync(SeasonId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, roundsAlreadyHeld).ToDictionary(number => number, RoundNumbered));
    }

    private static Season Season(int numberOfRounds) =>
        new(id: SeasonId, name: "2026/27", startDateUtc: StartDateUtc.AddMonths(-1),
            endDateUtc: StartDateUtc.AddMonths(9), isActive: true, numberOfRounds: numberOfRounds,
            competitionId: 3, passStandardPrice: null, passPremiumPrice: null);

    private static Round RoundNumbered(int number) =>
        new(id: number, seasonId: SeasonId, roundNumber: number, displayName: $"Gameweek {number}",
            startDateUtc: StartDateUtc, deadlineUtc: StartDateUtc.AddMinutes(-30), status: RoundStatus.Published,
            apiRoundName: null, lastReminderSentUtc: null, matches: null);

    /// <summary>Stands in for the identity the database assigns on insert.</summary>
    private static Round WithId(Round round)
    {
        typeof(Round).GetProperty(nameof(Round.Id))!.SetValue(round, CreatedRoundId);
        return round;
    }

    private static CreateMatchRequest MatchRequest(int homeTeamId, int awayTeamId, DateTime kickOffUtc, int? externalId = null) =>
        new() { HomeTeamId = homeTeamId, AwayTeamId = awayTeamId, MatchDateTimeUtc = kickOffUtc, ExternalId = externalId };

    private static CreateRoundCommand Command(int roundNumber = 5, params CreateMatchRequest[] matches) =>
        new(SeasonId, roundNumber, "Regular Season - 5", StartDateUtc, StartDateUtc.AddMinutes(-30), matches.ToList());

    private Task<Contracts.Admin.Rounds.RoundDto> HandleAsync(CreateRoundCommand? command = null) =>
        _handler.Handle(command ?? Command(), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldRequireAnAdministrator()
    {
        await HandleAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Handle_ShouldCreateTheRoundWithTheGivenSchedule()
    {
        Round? created = null;
        _rounds.CreateAsync(Arg.Do<Round>(r => created = r), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Round>()));

        await HandleAsync(Command(roundNumber: 7));

        created.Should().NotBeNull();
        created!.SeasonId.Should().Be(SeasonId);
        created.RoundNumber.Should().Be(7);
        created.StartDateUtc.Should().Be(StartDateUtc);
        created.DeadlineUtc.Should().Be(StartDateUtc.AddMinutes(-30));
        created.Status.Should().Be(RoundStatus.Draft, "a new round is not visible to players until published");
    }

    [Fact]
    public async Task Handle_ShouldNameTheRoundAfterItsNumber()
    {
        Round? created = null;
        _rounds.CreateAsync(Arg.Do<Round>(r => created = r), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Round>()));

        await HandleAsync(Command(roundNumber: 12));

        created!.DisplayName.Should().Be("Gameweek 12");
    }

    [Fact]
    public async Task Handle_ShouldAddEveryRequestedMatch()
    {
        Round? created = null;
        _rounds.CreateAsync(Arg.Do<Round>(r => created = r), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Round>()));

        await HandleAsync(Command(5,
            MatchRequest(101, 102, StartDateUtc),
            MatchRequest(103, 104, StartDateUtc.AddHours(2), externalId: 9001)));

        created!.Matches.Should().HaveCount(2);
        created.Matches.Should().Contain(m => m.ExternalId == 9001);
    }

    [Fact]
    public async Task Handle_ShouldCreateAnEmptyRound_WhenNoMatchesAreGiven()
    {
        var result = await HandleAsync(Command());

        result.MatchCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnTheSavedRound()
    {
        var result = await HandleAsync(Command(5, MatchRequest(101, 102, StartDateUtc)));

        result.Id.Should().Be(CreatedRoundId);
        result.SeasonId.Should().Be(SeasonId);
        result.RoundNumber.Should().Be(5);
        result.ApiRoundName.Should().Be("Regular Season - 5");
        result.Status.Should().Be(RoundStatus.Draft);
        result.MatchCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSaveTheRoundBeforeAddingMatchesToIt()
    {
        // Regression: a match is created against its round's id, so adding matches to an unsaved
        // round threw "Round ID must be greater than 0" and creating any round with fixtures failed.
        var order = new List<string>();
        _rounds.CreateAsync(Arg.Do<Round>(_ => order.Add("create")), Arg.Any<CancellationToken>())
            .Returns(ci => WithId(ci.Arg<Round>()));
        await _rounds.UpdateAsync(Arg.Do<Round>(_ => order.Add("update")), Arg.Any<CancellationToken>());
        order.Clear();

        var act = () => HandleAsync(Command(5, MatchRequest(101, 102, StartDateUtc)));

        await act.Should().NotThrowAsync();
        order.Should().Equal("create", "update");
    }

    [Fact]
    public async Task Handle_ShouldNotSaveTwice_WhenTheRoundHasNoMatches()
    {
        await HandleAsync(Command());

        await _rounds.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }
}

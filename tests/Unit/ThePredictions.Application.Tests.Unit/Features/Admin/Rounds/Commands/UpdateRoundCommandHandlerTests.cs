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
/// Editing a round from the admin screen. The fixture list sent up is the new truth: anything
/// missing from it is meant to be deleted - unless someone has already predicted it, which is the
/// one thing this must never destroy.
/// </summary>
public class UpdateRoundCommandHandlerTests
{
    private const int RoundId = 100;
    private const int SeasonId = 11;

    private static readonly DateTime StartDateUtc = new(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc);

    private readonly IRoundRepository _rounds = Substitute.For<IRoundRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly UpdateRoundCommandHandler _handler;

    public UpdateRoundCommandHandlerTests()
    {
        _handler = new UpdateRoundCommandHandler(_rounds, _currentUser);
        _rounds.GetMatchIdsWithPredictionsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private static Match Match(int id, DateTime kickOffUtc, int homeTeamId = 101, int awayTeamId = 102, int? externalId = null) =>
        new(id: id, roundId: RoundId, homeTeamId: homeTeamId, awayTeamId: awayTeamId,
            matchDateTimeUtc: kickOffUtc, customLockTimeUtc: null, status: MatchStatus.Scheduled,
            actualHomeTeamScore: null, actualAwayTeamScore: null, externalId: externalId,
            matchNumber: 1, placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

    private Round GivenRound(params Match[] matches)
    {
        var round = new Round(id: RoundId, seasonId: SeasonId, roundNumber: 5, displayName: "Gameweek 5",
            startDateUtc: StartDateUtc, deadlineUtc: StartDateUtc.AddMinutes(-30), status: RoundStatus.Draft,
            apiRoundName: "Regular Season - 5", lastReminderSentUtc: null,
            matches: matches.Length == 0 ? null : matches);

        _rounds.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns(round);
        return round;
    }

    private static MatchResultDtoStub MatchDto(int id, int homeTeamId, int awayTeamId, DateTime kickOffUtc, int? externalId = null) =>
        new(id, homeTeamId, awayTeamId, kickOffUtc, externalId);

    /// <summary>Mirrors the shape the command expects for each fixture row.</summary>
    internal sealed record MatchResultDtoStub(int Id, int HomeTeamId, int AwayTeamId, DateTime MatchDateTimeUtc, int? ExternalId);

    private static UpdateMatchRequest Request(MatchResultDtoStub stub) =>
        new()
        {
            Id = stub.Id,
            HomeTeamId = stub.HomeTeamId,
            AwayTeamId = stub.AwayTeamId,
            MatchDateTimeUtc = stub.MatchDateTimeUtc,
            ExternalId = stub.ExternalId
        };

    private static UpdateRoundCommand Command(params MatchResultDtoStub[] matches) =>
        new(RoundId, 5, "Regular Season - 5", StartDateUtc, StartDateUtc.AddMinutes(-30),
            RoundStatus.Draft, matches.Select(Request).ToList());

    private Task HandleAsync(UpdateRoundCommand? command = null) =>
        _handler.Handle(command ?? Command(), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldRequireAnAdministrator()
    {
        GivenRound();

        await HandleAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheRoundDoesNotExist()
    {
        _rounds.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns((Round?)null);

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldApplyTheEditedSchedule()
    {
        var round = GivenRound();
        var newStart = StartDateUtc.AddDays(1);

        await _handler.Handle(new UpdateRoundCommand(RoundId, 9, "Regular Season - 9", newStart,
            newStart.AddMinutes(-30), RoundStatus.Published, []), CancellationToken.None);

        round.RoundNumber.Should().Be(9);
        round.StartDateUtc.Should().Be(newStart);
        round.Status.Should().Be(RoundStatus.Published);
        await _rounds.Received(1).UpdateAsync(round, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUpdateAnExistingFixture()
    {
        var match = Match(1, StartDateUtc);
        var round = GivenRound(match);

        await HandleAsync(Command(MatchDto(1, 103, 104, StartDateUtc.AddHours(2))));

        match.HomeTeamId.Should().Be(103);
        match.AwayTeamId.Should().Be(104);
        match.MatchDateTimeUtc.Should().Be(StartDateUtc.AddHours(2));
    }

    [Fact]
    public async Task Handle_ShouldAddAFixtureSentWithNoId()
    {
        // Id zero is how the admin screen marks a brand-new row.
        var round = GivenRound(Match(1, StartDateUtc));

        await HandleAsync(Command(
            MatchDto(1, 101, 102, StartDateUtc),
            MatchDto(0, 105, 106, StartDateUtc.AddHours(2), externalId: 9001)));

        round.Matches.Should().HaveCount(2);
        round.Matches.Should().Contain(m => m.ExternalId == 9001);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreAnIdThatIsNotInTheRound()
    {
        var round = GivenRound(Match(1, StartDateUtc));

        await HandleAsync(Command(MatchDto(1, 101, 102, StartDateUtc), MatchDto(999, 105, 106, StartDateUtc)));

        round.Matches.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRemoveAFixtureLeftOutOfTheList()
    {
        var kept = Match(1, StartDateUtc);
        var dropped = Match(2, StartDateUtc, homeTeamId: 105, awayTeamId: 106);
        var round = GivenRound(kept, dropped);

        await HandleAsync(Command(MatchDto(1, 101, 102, StartDateUtc)));

        round.Matches.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuseToDeleteAFixtureThatHasPredictions()
    {
        // Deleting it would destroy players' predictions, so the whole edit is rejected rather
        // than silently dropping them.
        var kept = Match(1, StartDateUtc);
        var predicted = Match(2, StartDateUtc, homeTeamId: 105, awayTeamId: 106);
        GivenRound(kept, predicted);
        _rounds.GetMatchIdsWithPredictionsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns([2]);

        var act = () => HandleAsync(Command(MatchDto(1, 101, 102, StartDateUtc)));

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*already has user predictions*");
        await _rounds.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldNotAskAboutPredictions_WhenNothingIsBeingRemoved()
    {
        var round = GivenRound(Match(1, StartDateUtc));

        await HandleAsync(Command(MatchDto(1, 101, 102, StartDateUtc)));

        await _rounds.DidNotReceiveWithAnyArgs().GetMatchIdsWithPredictionsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldSaveTheRoundOnce()
    {
        var round = GivenRound(Match(1, StartDateUtc));

        await HandleAsync(Command(MatchDto(1, 101, 102, StartDateUtc)));

        await _rounds.Received(1).UpdateAsync(round, Arg.Any<CancellationToken>());
    }
}

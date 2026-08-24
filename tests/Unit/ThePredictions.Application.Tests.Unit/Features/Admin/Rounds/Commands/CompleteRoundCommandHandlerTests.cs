using FluentAssertions;
using MediatR;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Features.Badges.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

/// <summary>
/// Settling a finished round: prizes for every league in its season, then badges, then the results digest
/// and the prize emails - in that order, because each step reports what the one before it decided.
/// </summary>
public class CompleteRoundCommandHandlerTests
{
    private const int RoundId = 100;
    private const int SeasonId = 7;

    private static readonly DateTime FixedNow = new(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc);

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();

    private readonly CompleteRoundCommandHandler _handler;

    public CompleteRoundCommandHandlerTests()
    {
        _handler = new CompleteRoundCommandHandler(_mediator, _leagueRepository, _roundRepository);

        _leagueRepository.GetLeagueIdsForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([]);
        _mediator.Send(Arg.Any<EvaluateBadgesForRoundCommand>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private void GivenRound()
    {
        var round = new Round(
            id: RoundId, seasonId: SeasonId, roundNumber: 5, displayName: "Round 5",
            startDateUtc: FixedNow.AddDays(-2), deadlineUtc: FixedNow.AddDays(-1),
            status: RoundStatus.Completed, apiRoundName: null, lastReminderSentUtc: null, matches: []);

        _roundRepository.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns(round);
    }

    private Task HandleAsync() => _handler.Handle(new CompleteRoundCommand(RoundId), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheRoundDoesNotExist()
    {
        _roundRepository.GetByIdAsync(RoundId, Arg.Any<CancellationToken>()).Returns((Round?)null);

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldProcessPrizesForEveryLeagueInTheSeason()
    {
        GivenRound();
        _leagueRepository.GetLeagueIdsForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([11, 22]);

        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<ProcessPrizesCommand>(c => c.LeagueId == 11 && c.RoundId == RoundId), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<ProcessPrizesCommand>(c => c.LeagueId == 22 && c.RoundId == RoundId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSettlePrizesAndBadgesBeforeSendingTheDigest()
    {
        // The digest email reports each player's new position and celebrates the badges they just
        // earned, so it has to go out after both are finalised - and the prize emails after it.
        GivenRound();
        _leagueRepository.GetLeagueIdsForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns([11]);

        await HandleAsync();

        Received.InOrder(() =>
        {
            _mediator.Send(Arg.Any<ProcessPrizesCommand>(), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Any<EvaluateBadgesForRoundCommand>(), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Any<SendRoundDigestEmailsCommand>(), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Any<SendPrizeNotificationsCommand>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ShouldPassTheNewBadgesToTheDigest()
    {
        GivenRound();
        IReadOnlyList<RoundBadgeAward> awarded = [new("user-1", "marksman-1")];
        _mediator.Send(Arg.Any<EvaluateBadgesForRoundCommand>(), Arg.Any<CancellationToken>()).Returns(awarded);

        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<SendRoundDigestEmailsCommand>(c => c.BadgesAwarded == awarded), Arg.Any<CancellationToken>());
    }
}

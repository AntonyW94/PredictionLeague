using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

public class PublishUpcomingRoundsCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(Now);
    private readonly PublishUpcomingRoundsCommandHandler _handler;

    public PublishUpcomingRoundsCommandHandlerTests()
    {
        _handler = new PublishUpcomingRoundsCommandHandler(
            _roundRepository,
            _dateTimeProvider,
            Substitute.For<ILogger<PublishUpcomingRoundsCommandHandler>>());

        // Default every read to an empty set; individual tests override as needed.
        _roundRepository.GetDraftRoundsStartingBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round>());
        _roundRepository.GetPublishedRoundsStartingAfterAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round>());
        _roundRepository.GetPublishedRoundsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round>());
    }

    private static Match ConfirmedMatch(int roundId) =>
        Match.Create(roundId, homeTeamId: 10, awayTeamId: 20, matchDateTimeUtc: Now.AddDays(5), externalId: 100);

    private static Match PlaceholderMatch(int roundId) =>
        Match.CreatePlaceholder(roundId, "TBD", "TBD", "Round of 16");

    private static Round Round(int id, RoundStatus status, params Match[] matches) =>
        new(id, seasonId: 1, roundNumber: id, displayName: $"Round {id}",
            startDateUtc: Now.AddDays(5), deadlineUtc: Now.AddDays(5).AddMinutes(-30),
            status: status, apiRoundName: null, lastReminderSentUtc: null, matches: matches);

    [Fact]
    public async Task Handle_ShouldUnpublishRound_WhenPublishedRoundHasNoConfirmedFixtures()
    {
        var round = Round(7, RoundStatus.Published, PlaceholderMatch(7), PlaceholderMatch(7));
        _roundRepository.GetPublishedRoundsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round> { [round.Id] = round });

        await _handler.Handle(new PublishUpcomingRoundsCommand(), CancellationToken.None);

        round.Status.Should().Be(RoundStatus.Draft);
        await _roundRepository.Received(1).UpdateAsync(round, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLeaveRoundPublished_WhenPublishedRoundHasAtLeastOneConfirmedFixture()
    {
        var round = Round(3, RoundStatus.Published, ConfirmedMatch(3), PlaceholderMatch(3));
        _roundRepository.GetPublishedRoundsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round> { [round.Id] = round });

        await _handler.Handle(new PublishUpcomingRoundsCommand(), CancellationToken.None);

        round.Status.Should().Be(RoundStatus.Published);
        await _roundRepository.DidNotReceive().UpdateAsync(Arg.Any<Round>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPublishDraftRound_WhenItHasConfirmedFixtures()
    {
        var round = Round(1, RoundStatus.Draft, ConfirmedMatch(1));
        _roundRepository.GetDraftRoundsStartingBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round> { [round.Id] = round });

        await _handler.Handle(new PublishUpcomingRoundsCommand(), CancellationToken.None);

        round.Status.Should().Be(RoundStatus.Published);
        await _roundRepository.Received(1).UpdateAsync(round, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotPublishDraftRound_WhenItHasNoConfirmedFixtures()
    {
        var round = Round(4, RoundStatus.Draft, PlaceholderMatch(4));
        _roundRepository.GetDraftRoundsStartingBeforeAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round> { [round.Id] = round });

        await _handler.Handle(new PublishUpcomingRoundsCommand(), CancellationToken.None);

        round.Status.Should().Be(RoundStatus.Draft);
        await _roundRepository.DidNotReceive().UpdateAsync(Arg.Any<Round>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPullBackARoundThatMovedBeyondTheSixWeekWindow()
    {
        // Fixtures get rescheduled. A published round whose start date moves out past the window
        // has to go back to draft, or it stays visible months early.
        var round = Round(9, RoundStatus.Published, ConfirmedMatch(9));
        _roundRepository.GetPublishedRoundsStartingAfterAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round> { [9] = round });

        await _handler.Handle(new PublishUpcomingRoundsCommand(), CancellationToken.None);

        round.Status.Should().Be(RoundStatus.Draft);
        await _roundRepository.Received(1).UpdateAsync(round, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPullBackEveryDistantRound()
    {
        var first = Round(9, RoundStatus.Published, ConfirmedMatch(9));
        var second = Round(10, RoundStatus.Published, ConfirmedMatch(10));
        _roundRepository.GetPublishedRoundsStartingAfterAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Round> { [9] = first, [10] = second });

        await _handler.Handle(new PublishUpcomingRoundsCommand(), CancellationToken.None);

        first.Status.Should().Be(RoundStatus.Draft);
        second.Status.Should().Be(RoundStatus.Draft);
    }
}

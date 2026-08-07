using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

/// <summary>
/// The league administrator ticking off "I have paid this winner". Only they can do it, only once
/// the season has actually finished, and only for someone who has winnings to collect.
/// </summary>
public class MarkLeaguePayoutPaidCommandHandlerTests
{
    private const int LeagueId = 7;
    private const int SeasonId = 11;
    private const string AdministratorId = "admin-1";
    private const string WinnerUserId = "winner-1";

    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly IWinningsRepository _winningsRepository = Substitute.For<IWinningsRepository>();
    private readonly ILeaguePayoutRepository _payoutRepository = Substitute.For<ILeaguePayoutRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly MarkLeaguePayoutPaidCommandHandler _handler;

    public MarkLeaguePayoutPaidCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _handler = new MarkLeaguePayoutPaidCommandHandler(
            _leagueRepository, _roundRepository, _winningsRepository, _payoutRepository, _dateTimeProvider);

        GivenLeague();
        GivenRounds(RoundStatus.Completed);
        _winningsRepository.GetUserLeagueTotalAsync(LeagueId, WinnerUserId, Arg.Any<CancellationToken>()).Returns(50m);
    }

    private void GivenLeague(string administratorUserId = AdministratorId) =>
        _leagueRepository.GetByIdAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(
            new League(id: LeagueId, name: "The Office League", seasonId: SeasonId,
                administratorUserId: administratorUserId, entryCode: "ABC123",
                createdAtUtc: NowUtc.AddMonths(-10), entryDeadlineUtc: NowUtc.AddMonths(-9),
                pointsForExactScore: 3, pointsForCorrectResult: 1, price: 10m, isFree: false,
                hasPrizes: true, prizeFundOverride: null, members: [], prizeSettings: []));

    private void GivenNoLeague() =>
        _leagueRepository.GetByIdAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((League?)null);

    private void GivenRounds(params RoundStatus[] statuses) =>
        _roundRepository.GetAllForSeasonAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(
            statuses.Select((status, index) => new Round(
                    id: index + 1, seasonId: SeasonId, roundNumber: index + 1, displayName: $"Gameweek {index + 1}",
                    startDateUtc: NowUtc.AddMonths(-8).AddDays(index * 7), deadlineUtc: NowUtc.AddMonths(-8).AddDays(index * 7).AddMinutes(-30),
                    status: status, apiRoundName: null, lastReminderSentUtc: null, matches: null))
                .ToDictionary(r => r.Id));

    private LeaguePayout CapturedPayout() =>
        (LeaguePayout)_payoutRepository.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(ILeaguePayoutRepository.UpsertAsync))
            .GetArguments()[0]!;

    private Task HandleAsync(string requestingUserId = AdministratorId, string winnerUserId = WinnerUserId) =>
        _handler.Handle(new MarkLeaguePayoutPaidCommand(LeagueId, winnerUserId, requestingUserId), CancellationToken.None);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldRefuseAnEmptyWinner(string? winnerUserId)
    {
        var act = () => HandleAsync(winnerUserId: winnerUserId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        GivenNoLeague();

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnyoneButTheLeagueAdministrator()
    {
        // Being a site admin is not enough - this is the league owner's own record of who they paid.
        var act = () => HandleAsync(requestingUserId: "someone-else");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhileAnyRoundIsStillOutstanding()
    {
        GivenRounds(RoundStatus.Completed, RoundStatus.InProgress);

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*until the season is complete*");
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheSeasonHasNoRoundsAtAll()
    {
        // An empty season is not a finished one - every round being complete is vacuously true, so
        // the count is checked separately.
        GivenRounds();

        var act = () => HandleAsync();

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenThePlayerHasNothingToCollect()
    {
        _winningsRepository.GetUserLeagueTotalAsync(LeagueId, WinnerUserId, Arg.Any<CancellationToken>()).Returns(0m);

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*no winnings to pay out*");
        await _payoutRepository.DidNotReceiveWithAnyArgs().UpsertAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheFirstPayoutAtTheAmountTheyWon()
    {
        _winningsRepository.GetUserLeagueTotalAsync(LeagueId, WinnerUserId, Arg.Any<CancellationToken>()).Returns(125.50m);

        await HandleAsync();

        var payout = CapturedPayout();
        payout.LeagueId.Should().Be(LeagueId);
        payout.UserId.Should().Be(WinnerUserId);
        payout.TotalAmount.Should().Be(125.50m);
        payout.PaidAtUtc.Should().Be(NowUtc);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheOriginalAmount_WhenAPayoutWasAlreadyRecorded()
    {
        // Marking an existing record paid must not re-read and overwrite the amount, or a later
        // change to winnings would silently rewrite what was agreed.
        var existing = new LeaguePayout(id: 1, leagueId: LeagueId, userId: WinnerUserId, totalAmount: 80m,
            paidAtUtc: null, createdAtUtc: NowUtc.AddDays(-1), updatedAtUtc: NowUtc.AddDays(-1));
        _payoutRepository.GetByLeagueAndUserAsync(LeagueId, WinnerUserId, Arg.Any<CancellationToken>()).Returns(existing);

        await HandleAsync();

        var payout = CapturedPayout();
        payout.Should().BeSameAs(existing);
        payout.TotalAmount.Should().Be(80m);
        payout.IsPaid.Should().BeTrue();
        await _winningsRepository.DidNotReceiveWithAnyArgs().GetUserLeagueTotalAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldSaveThePayout()
    {
        await HandleAsync();

        await _payoutRepository.Received(1).UpsertAsync(Arg.Any<LeaguePayout>(), Arg.Any<CancellationToken>());
    }
}

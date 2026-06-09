using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

public class SendPrizeNotificationsCommandHandlerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IPrizeNotificationRepository _prizeNotificationRepository = Substitute.For<IPrizeNotificationRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly SendPrizeNotificationsCommandHandler _handler;

    private const long TemplateId = 12;

    public SendPrizeNotificationsCommandHandlerTests()
    {
        var brevo = Options.Create(new BrevoSettings { Templates = new TemplateSettings { PrizeWon = TemplateId } });
        var site = Options.Create(new SiteSettings { BaseUrl = "https://test.local" });
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 6, 9, 8, 0, 0, DateTimeKind.Utc));

        _handler = new SendPrizeNotificationsCommandHandler(
            _mediator, _roundRepository, _emailService, _prizeNotificationRepository, brevo, site, _dateTimeProvider,
            Substitute.For<ILogger<SendPrizeNotificationsCommandHandler>>());
    }

    private static Round CompletedRound() =>
        new(id: 7, seasonId: 1, roundNumber: 7, displayName: "Gameweek 7",
            startDateUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            deadlineUtc: new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
            status: RoundStatus.Completed, apiRoundName: null, lastReminderSentUtc: null, matches: null);

    private void GivenWinners(params PrizeWinner[] winners) =>
        _mediator.Send(Arg.Any<GetPrizeWinnersForRoundQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PrizeWinner>)winners.ToList());

    private static WonPrize Won(bool alreadyNotified = false, int settingId = 1, int? roundNumber = 7, int? month = null) =>
        new(LeagueId: 5, LeagueName: "Office League", LeaguePrizeSettingId: settingId, PrizeType: PrizeType.Round,
            PrizeDescription: null, Rank: 1, Stage: null, Amount: 10m, RoundNumber: roundNumber, Month: month,
            PrizeRoundName: "Gameweek 7", AlreadyNotified: alreadyNotified);

    private static PrizeWinner Winner(string email, params WonPrize[] prizes) =>
        new("u1", email, "Antony", "Gameweek 7", prizes.ToList());

    [Fact]
    public async Task Handle_ShouldSendAndRecord_WhenWinnerHasUnnotifiedPrize()
    {
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());
        GivenWinners(Winner("antony@example.com", Won()));

        List<PrizeNotification>? recorded = null;
        _prizeNotificationRepository
            .AddNotificationsAsync(Arg.Do<IEnumerable<PrizeNotification>>(x => recorded = x.ToList()), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SendPrizeNotificationsCommand(7), CancellationToken.None);

        await _emailService.Received(1).SendTemplatedEmailAsync("antony@example.com", TemplateId, Arg.Any<object>());
        recorded.Should().ContainSingle();
        recorded![0].UserId.Should().Be("u1");
        recorded[0].LeaguePrizeSettingId.Should().Be(1);
        recorded[0].RoundNumber.Should().Be(7);
    }

    [Fact]
    public async Task Handle_ShouldSendOneGroupedEmail_WhenWinnerHasMultiplePrizes()
    {
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());
        GivenWinners(Winner("antony@example.com", Won(settingId: 1), Won(settingId: 2, roundNumber: null, month: 6)));

        List<PrizeNotification>? recorded = null;
        _prizeNotificationRepository
            .AddNotificationsAsync(Arg.Do<IEnumerable<PrizeNotification>>(x => recorded = x.ToList()), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SendPrizeNotificationsCommand(7), CancellationToken.None);

        await _emailService.Received(1).SendTemplatedEmailAsync("antony@example.com", TemplateId, Arg.Any<object>());
        recorded.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldSkipWinner_WhenAllPrizesAlreadyNotifiedAndNotForced()
    {
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());
        GivenWinners(Winner("antony@example.com", Won(alreadyNotified: true)));

        await _handler.Handle(new SendPrizeNotificationsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
        await _prizeNotificationRepository.DidNotReceiveWithAnyArgs().AddNotificationsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldOnlyRecordUnnotifiedPrizes_WhenWinnerHasMix()
    {
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());
        GivenWinners(Winner("antony@example.com", Won(settingId: 1, alreadyNotified: true), Won(settingId: 2, alreadyNotified: false)));

        List<PrizeNotification>? recorded = null;
        _prizeNotificationRepository
            .AddNotificationsAsync(Arg.Do<IEnumerable<PrizeNotification>>(x => recorded = x.ToList()), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SendPrizeNotificationsCommand(7), CancellationToken.None);

        await _emailService.Received(1).SendTemplatedEmailAsync("antony@example.com", TemplateId, Arg.Any<object>());
        recorded.Should().ContainSingle();
        recorded![0].LeaguePrizeSettingId.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldResendButRecordNothingNew_WhenForcedAndAllAlreadyNotified()
    {
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());
        GivenWinners(Winner("antony@example.com", Won(alreadyNotified: true)));

        await _handler.Handle(new SendPrizeNotificationsCommand(7, Force: true), CancellationToken.None);

        await _emailService.Received(1).SendTemplatedEmailAsync("antony@example.com", TemplateId, Arg.Any<object>());
        await _prizeNotificationRepository.DidNotReceiveWithAnyArgs().AddNotificationsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenRoundNotCompleted()
    {
        var round = new Round(id: 7, seasonId: 1, roundNumber: 7, displayName: "Gameweek 7",
            startDateUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            deadlineUtc: new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
            status: RoundStatus.InProgress, apiRoundName: null, lastReminderSentUtc: null, matches: null);
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(round);

        await _handler.Handle(new SendPrizeNotificationsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenRoundNotFound()
    {
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns((Round?)null);

        await _handler.Handle(new SendPrizeNotificationsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldNotSend_WhenTemplateNotConfigured()
    {
        var brevo = Options.Create(new BrevoSettings { Templates = new TemplateSettings { PrizeWon = 0 } });
        var handler = new SendPrizeNotificationsCommandHandler(
            _mediator, _roundRepository, _emailService, _prizeNotificationRepository, brevo,
            Options.Create(new SiteSettings { BaseUrl = "https://test.local" }), _dateTimeProvider,
            Substitute.For<ILogger<SendPrizeNotificationsCommandHandler>>());

        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());

        await handler.Handle(new SendPrizeNotificationsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }
}

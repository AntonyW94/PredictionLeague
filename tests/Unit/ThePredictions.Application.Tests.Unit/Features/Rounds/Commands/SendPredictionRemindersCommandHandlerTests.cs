using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Rounds.Commands;
using ThePredictions.Application.Formatters;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Rounds.Commands;

public class SendPredictionRemindersCommandHandlerTests
{
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly IReminderService _reminderService = Substitute.For<IReminderService>();
    private readonly IPredictionReminderNotificationRepository _notificationRepository = Substitute.For<IPredictionReminderNotificationRepository>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IEmailDateFormatter _dateFormatter = Substitute.For<IEmailDateFormatter>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly SendPredictionRemindersCommandHandler _handler;

    private const long TemplateId = 9;
    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    public SendPredictionRemindersCommandHandlerTests()
    {
        var brevo = Options.Create(new BrevoSettings { Templates = new TemplateSettings { PredictionsMissing = TemplateId } });
        var site = Options.Create(new SiteSettings { BaseUrl = "https://test.local" });
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _dateFormatter.FormatDeadline(Arg.Any<DateTime>()).Returns("Tuesday");
        _notificationRepository.GetLastRemindedUtcAsync(Arg.Any<int>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DateTime>());

        _handler = new SendPredictionRemindersCommandHandler(
            _roundRepository, _reminderService, _notificationRepository, _membershipService,
            _emailService, _dateFormatter, brevo, site, _dateTimeProvider,
            Substitute.For<ILogger<SendPredictionRemindersCommandHandler>>());
    }

    private static Round OpenRound() =>
        new(id: 43, seasonId: 2, roundNumber: 5, displayName: "Round of 16",
            startDateUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            deadlineUtc: new DateTime(2026, 7, 7, 16, 30, 0, DateTimeKind.Utc),
            status: RoundStatus.InProgress, apiRoundName: null, lastReminderSentUtc: null, matches: null);

    private static ChaseUserDto Chase(string userId, string email = "p@example.com") =>
        new(email, "Player", "Round of 16", new DateTime(2026, 7, 7, 16, 30, 0, DateTimeKind.Utc), userId);

    private void GivenMissing(params ChaseUserDto[] users) =>
        _reminderService.GetUsersMissingPredictionsAsync(43, NowUtc, Arg.Any<CancellationToken>())
            .Returns(users.ToList());

    private SendPredictionRemindersCommand AdminCommand(params string[] userIds) =>
        new(43, LeagueId: null, userIds.ToList(), "admin-1", IsSiteAdmin: true);

    [Fact]
    public async Task Handle_ShouldSendAndRecord_WhenPlayerIsMissingPredictions()
    {
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(OpenRound());
        GivenMissing(Chase("user-1"));

        var result = await _handler.Handle(AdminCommand("user-1"), CancellationToken.None);

        result.SentCount.Should().Be(1);
        await _emailService.Received(1).SendTemplatedEmailAsync("p@example.com", TemplateId, Arg.Any<object>());
        await _notificationRepository.Received(1).UpsertAsync(
            Arg.Is<PredictionReminderNotification>(n => n.RoundId == 43 && n.UserId == "user-1" && n.RemindedByUserId == "admin-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipAsNoLongerMissing_WhenRequestedPlayerHasNoMissingFixtures()
    {
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(OpenRound());
        GivenMissing(Chase("user-1"));

        var result = await _handler.Handle(AdminCommand("user-1", "user-complete"), CancellationToken.None);

        result.SentCount.Should().Be(1);
        result.SkippedNoLongerMissingCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldSkipAsRecentlyReminded_WhenWithinThrottleWindow()
    {
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(OpenRound());
        GivenMissing(Chase("user-1"));
        _notificationRepository.GetLastRemindedUtcAsync(43, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DateTime> { ["user-1"] = NowUtc.AddHours(-2) });

        var result = await _handler.Handle(AdminCommand("user-1"), CancellationToken.None);

        result.SentCount.Should().Be(0);
        result.SkippedRecentlyRemindedCount.Should().Be(1);
        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldSend_WhenLastReminderIsOlderThanThrottleWindow()
    {
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(OpenRound());
        GivenMissing(Chase("user-1"));
        _notificationRepository.GetLastRemindedUtcAsync(43, Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DateTime> { ["user-1"] = NowUtc.AddHours(-7) });

        var result = await _handler.Handle(AdminCommand("user-1"), CancellationToken.None);

        result.SentCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenDeadlineHasPassed()
    {
        var passedRound = new Round(id: 43, seasonId: 2, roundNumber: 5, displayName: "Round of 16",
            startDateUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            deadlineUtc: NowUtc.AddHours(-1),
            status: RoundStatus.InProgress, apiRoundName: null, lastReminderSentUtc: null, matches: null);
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(passedRound);

        var act = () => _handler.Handle(AdminCommand("user-1"), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenRoundNotFound()
    {
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns((Round?)null);

        var act = () => _handler.Handle(AdminCommand("user-1"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnZero_WhenNoUserIdsRequested()
    {
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(OpenRound());

        var result = await _handler.Handle(AdminCommand(), CancellationToken.None);

        result.SentCount.Should().Be(0);
        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorised_WhenGlobalSendByNonAdmin()
    {
        var command = new SendPredictionRemindersCommand(43, LeagueId: null, ["user-1"], "user-x", IsSiteAdmin: false);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldEnforceLeagueOwnership_WhenLeagueScopedSendByNonAdmin()
    {
        _membershipService.EnsureLeagueAdministratorAsync(10, "user-x", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));
        var command = new SendPredictionRemindersCommand(43, LeagueId: 10, ["user-1"], "user-x", IsSiteAdmin: false);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTemplateNotConfigured()
    {
        var brevo = Options.Create(new BrevoSettings { Templates = new TemplateSettings { PredictionsMissing = 0 } });
        var handler = new SendPredictionRemindersCommandHandler(
            _roundRepository, _reminderService, _notificationRepository, _membershipService,
            _emailService, _dateFormatter, brevo, Options.Create(new SiteSettings()), _dateTimeProvider,
            Substitute.For<ILogger<SendPredictionRemindersCommandHandler>>());
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(OpenRound());

        var act = () => handler.Handle(AdminCommand("user-1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNoTemplatesAreConfiguredAtAll()
    {
        var brevo = Options.Create(new BrevoSettings { Templates = null });
        var handler = new SendPredictionRemindersCommandHandler(
            _roundRepository, _reminderService, _notificationRepository, _membershipService,
            _emailService, _dateFormatter, brevo, Options.Create(new SiteSettings()), _dateTimeProvider,
            Substitute.For<ILogger<SendPredictionRemindersCommandHandler>>());
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(OpenRound());

        var act = () => handler.Handle(AdminCommand("user-1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_ShouldSkipTheOwnershipCheck_WhenALeagueScopedSendIsMadeByASiteAdmin()
    {
        // A site admin can chase anyone, so their own league membership is never consulted.
        _roundRepository.GetByIdAsync(43, Arg.Any<CancellationToken>()).Returns(OpenRound());
        GivenMissing(Chase("user-1"));
        var command = new SendPredictionRemindersCommand(43, LeagueId: 10, ["user-1"], "admin-1", IsSiteAdmin: true);

        await _handler.Handle(command, CancellationToken.None);

        await _membershipService.DidNotReceiveWithAnyArgs()
            .EnsureLeagueAdministratorAsync(default, default!, CancellationToken.None);
    }
}

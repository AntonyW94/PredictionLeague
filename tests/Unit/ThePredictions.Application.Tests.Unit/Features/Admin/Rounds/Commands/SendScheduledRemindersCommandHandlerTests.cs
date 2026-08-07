using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Formatters;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

/// <summary>
/// Runs every half hour and chases players who have not predicted yet. The guards matter: sending
/// twice for the same round would spam everyone, and sending with no template configured would
/// fail silently per user.
/// </summary>
public class SendScheduledRemindersCommandHandlerTests
{
    private const int RoundId = 100;
    private const long TemplateId = 42;

    private static readonly DateTime NowUtc = new(2026, 5, 28, 10, 0, 0, DateTimeKind.Utc);

    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IReminderService _reminderService = Substitute.For<IReminderService>();
    private readonly IEmailDateFormatter _dateFormatter = Substitute.For<IEmailDateFormatter>();

    private SendScheduledRemindersCommandHandler BuildHandler(long templateId = TemplateId, string? baseUrl = "https://www.thepredictions.co.uk")
    {
        _dateFormatter.FormatDeadline(Arg.Any<DateTime>()).Returns("Saturday, 30 May 2026 at 15:00 (BST)");

        return new SendScheduledRemindersCommandHandler(
            _roundRepository, _emailService, _reminderService, _dateFormatter,
            Options.Create(new BrevoSettings { Templates = new TemplateSettings { PredictionsMissing = templateId } }),
            Options.Create(new SiteSettings { BaseUrl = baseUrl }),
            new TestDateTimeProvider(NowUtc),
            NullLogger<SendScheduledRemindersCommandHandler>.Instance);
    }

    private Round GivenNextRound()
    {
        var round = new Round(
            id: RoundId, seasonId: 1, roundNumber: 5, displayName: "Round 5",
            startDateUtc: NowUtc.AddDays(1), deadlineUtc: NowUtc.AddDays(2),
            status: RoundStatus.Published, apiRoundName: null, lastReminderSentUtc: null, matches: null);

        _roundRepository.GetNextRoundForReminderAsync(Arg.Any<CancellationToken>()).Returns(round);
        return round;
    }

    private void GivenDue(bool due = true) =>
        _reminderService.ShouldSendReminderAsync(Arg.Any<Round>(), NowUtc, Arg.Any<CancellationToken>()).Returns(due);

    private void GivenUsersToChase(params ChaseUserDto[] users) =>
        _reminderService.GetUsersMissingPredictionsAsync(RoundId, NowUtc, Arg.Any<CancellationToken>())
            .Returns(users.ToList());

    private static ChaseUserDto User(string id = "user-1", DateTime? deadlineUtc = null) =>
        new($"{id}@example.com", "Alex", "Round 5", deadlineUtc ?? NowUtc.AddHours(6), id);

    private Task HandleAsync(SendScheduledRemindersCommandHandler? handler = null) =>
        (handler ?? BuildHandler()).Handle(new SendScheduledRemindersCommand(), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenThereIsNoUpcomingRound()
    {
        _roundRepository.GetNextRoundForReminderAsync(Arg.Any<CancellationToken>()).Returns((Round?)null);

        await HandleAsync();

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheRoundIsNotDueAReminderYet()
    {
        GivenNextRound();
        GivenDue(false);

        await HandleAsync();

        await _reminderService.DidNotReceiveWithAnyArgs().GetUsersMissingPredictionsAsync(default, default, CancellationToken.None);
        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldNotStampTheRound_WhenEveryoneHasAlreadyPredicted()
    {
        // Nothing was sent, so the round must stay eligible for a later reminder.
        var round = GivenNextRound();
        GivenDue();
        GivenUsersToChase();

        await HandleAsync();

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
        await _roundRepository.DidNotReceiveWithAnyArgs().UpdateLastReminderSentAsync(default!, CancellationToken.None);
        round.LastReminderSentUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    public async Task Handle_ShouldSendNothing_WhenTheTemplateIsNotConfigured(long templateId)
    {
        GivenNextRound();
        GivenDue();
        GivenUsersToChase(User());

        await HandleAsync(BuildHandler(templateId));

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
        await _roundRepository.DidNotReceiveWithAnyArgs().UpdateLastReminderSentAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldEmailEveryPlayerWhoStillOwesPredictions()
    {
        GivenNextRound();
        GivenDue();
        GivenUsersToChase(User("user-1"), User("user-2"));

        await HandleAsync();

        await _emailService.Received(1).SendTemplatedEmailAsync("user-1@example.com", TemplateId, Arg.Any<object>());
        await _emailService.Received(1).SendTemplatedEmailAsync("user-2@example.com", TemplateId, Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldStampTheRound_SoTheNextRunDoesNotChaseAgain()
    {
        var round = GivenNextRound();
        GivenDue();
        GivenUsersToChase(User());

        await HandleAsync();

        round.LastReminderSentUtc.Should().Be(NowUtc);
        await _roundRepository.Received(1).UpdateLastReminderSentAsync(round, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLinkStraightToThePredictionsPageForThatRound()
    {
        GivenNextRound();
        GivenDue();
        GivenUsersToChase(User());
        object? captured = null;
        await _emailService.SendTemplatedEmailAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Do<object>(p => captured = p));

        await HandleAsync();

        Value(captured!, "PREDICTIONS_URL").Should().Be($"https://www.thepredictions.co.uk/predictions/{RoundId}");
    }

    [Fact]
    public async Task Handle_ShouldFallBackToTheCanonicalSite_WhenNoBaseUrlIsConfigured()
    {
        // Background jobs have no request to derive a host from, and a header is attacker-controlled.
        GivenNextRound();
        GivenDue();
        GivenUsersToChase(User());
        object? captured = null;
        await _emailService.SendTemplatedEmailAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Do<object>(p => captured = p));

        await HandleAsync(BuildHandler(baseUrl: null));

        Value(captured!, "PREDICTIONS_URL").Should().Be($"{SiteSettings.FallbackBaseUrl}/predictions/{RoundId}");
    }

    [Fact]
    public async Task Handle_ShouldPersonaliseTheEmailAndFormatTheDeadline()
    {
        GivenNextRound();
        GivenDue();
        GivenUsersToChase(User());
        object? captured = null;
        await _emailService.SendTemplatedEmailAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Do<object>(p => captured = p));

        await HandleAsync();

        Value(captured!, "FIRST_NAME").Should().Be("Alex");
        Value(captured!, "ROUND_NAME").Should().Be("Round 5");
        Value(captured!, "DEADLINE").Should().Be("Saturday, 30 May 2026 at 15:00 (BST)");
    }

    [Fact]
    public async Task Handle_ShouldSetTheUrgencyFromEachPlayersOwnDeadline()
    {
        // Different leagues can have different deadlines for the same round, so urgency is
        // per-player rather than per-round.
        GivenNextRound();
        GivenDue();
        var captured = new List<object>();
        await _emailService.SendTemplatedEmailAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Do<object>(captured.Add));
        GivenUsersToChase(
            User("soon", NowUtc.AddMinutes(30)),
            User("later", NowUtc.AddDays(2)));

        await HandleAsync();

        captured.Should().HaveCount(2);
        Value(captured[0], "URGENCY").Should().NotBe(Value(captured[1], "URGENCY"));
    }

    private static string? Value(object parameters, string propertyName) =>
        parameters.GetType().GetProperty(propertyName)?.GetValue(parameters)?.ToString();
}

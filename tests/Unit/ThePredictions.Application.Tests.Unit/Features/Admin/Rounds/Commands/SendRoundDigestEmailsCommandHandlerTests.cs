using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Formatters;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

public class SendRoundDigestEmailsCommandHandlerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IEmailDateFormatter _dateFormatter = Substitute.For<IEmailDateFormatter>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly SendRoundDigestEmailsCommandHandler _handler;

    public SendRoundDigestEmailsCommandHandlerTests()
    {
        var brevo = Options.Create(new BrevoSettings { Templates = new TemplateSettings { RoundResultsDigest = 11 } });
        var site = Options.Create(new SiteSettings { BaseUrl = "https://test.local" });
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 6, 9, 8, 0, 0, DateTimeKind.Utc));
        _dateFormatter.FormatDeadline(Arg.Any<DateTime>()).Returns("formatted");

        _handler = new SendRoundDigestEmailsCommandHandler(
            _mediator, _roundRepository, _emailService, _dateFormatter, brevo, site, _dateTimeProvider,
            Substitute.For<ILogger<SendRoundDigestEmailsCommandHandler>>());
    }

    private static Round CompletedRound(DateTime? digestSentUtc = null) =>
        new(id: 7, seasonId: 1, roundNumber: 7, displayName: "Gameweek 7",
            startDateUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            deadlineUtc: new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
            status: RoundStatus.Completed, apiRoundName: null, lastReminderSentUtc: null,
            matches: null, resultsDigestSentUtc: digestSentUtc);

    private void GivenDigests(params UserRoundDigest[] digests) =>
        _mediator.Send(Arg.Any<GetRoundDigestQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<UserRoundDigest>)digests.ToList());

    private static UserRoundDigest Digest(string email) =>
        new("u1", email, "Antony", "Gameweek 7", 2, 6, "Gameweek 8", null,
            new List<LeagueRoundDigest> { new(5, "Office League", 18, 3, 1, "Sarah J", 24) });

    [Fact]
    public async Task Handle_ShouldSendAndMarkSent_WhenRoundCompletedAndNotYetSent()
    {
        var round = CompletedRound();
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(round);
        GivenDigests(Digest("antony@example.com"));

        await _handler.Handle(new SendRoundDigestEmailsCommand(7), CancellationToken.None);

        await _emailService.Received(1).SendTemplatedEmailAsync("antony@example.com", 11, Arg.Any<object>());
        await _roundRepository.Received(1).UpdateResultsDigestSentAsync(round, Arg.Any<CancellationToken>());
        round.ResultsDigestSentUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenAlreadySentAndNotForced()
    {
        var round = CompletedRound(digestSentUtc: new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc));
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(round);

        await _handler.Handle(new SendRoundDigestEmailsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
        await _roundRepository.DidNotReceiveWithAnyArgs().UpdateResultsDigestSentAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldResend_WhenAlreadySentButForced()
    {
        var round = CompletedRound(digestSentUtc: new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc));
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(round);
        GivenDigests(Digest("antony@example.com"));

        await _handler.Handle(new SendRoundDigestEmailsCommand(7, Force: true), CancellationToken.None);

        await _emailService.Received(1).SendTemplatedEmailAsync("antony@example.com", 11, Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenRoundNotCompleted()
    {
        var round = new Round(id: 7, seasonId: 1, roundNumber: 7, displayName: "Gameweek 7",
            startDateUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            deadlineUtc: new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
            status: RoundStatus.InProgress, apiRoundName: null, lastReminderSentUtc: null, matches: null);
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(round);

        await _handler.Handle(new SendRoundDigestEmailsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldSkip_WhenRoundNotFound()
    {
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns((Round?)null);

        await _handler.Handle(new SendRoundDigestEmailsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldNotSend_WhenTemplateNotConfigured()
    {
        var brevo = Options.Create(new BrevoSettings { Templates = new TemplateSettings { RoundResultsDigest = 0 } });
        var handler = new SendRoundDigestEmailsCommandHandler(
            _mediator, _roundRepository, _emailService, _dateFormatter, brevo,
            Options.Create(new SiteSettings { BaseUrl = "https://test.local" }), _dateTimeProvider,
            Substitute.For<ILogger<SendRoundDigestEmailsCommandHandler>>());

        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());

        await handler.Handle(new SendRoundDigestEmailsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldNotSend_WhenNoTemplatesAreConfiguredAtAll()
    {
        var brevo = Options.Create(new BrevoSettings { Templates = null });
        var handler = new SendRoundDigestEmailsCommandHandler(
            _mediator, _roundRepository, _emailService, _dateFormatter, brevo,
            Options.Create(new SiteSettings { BaseUrl = "https://test.local" }), _dateTimeProvider,
            Substitute.For<ILogger<SendRoundDigestEmailsCommandHandler>>());

        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());

        await handler.Handle(new SendRoundDigestEmailsCommand(7), CancellationToken.None);

        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    private object CapturedParameters(string email = "antony@example.com") =>
        _emailService.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IEmailService.SendTemplatedEmailAsync)
                         && (string)c.GetArguments()[0]! == email)
            .GetArguments()[2]!;

    private static T Property<T>(object parameters, string name) =>
        (T)parameters.GetType().GetProperty(name)!.GetValue(parameters)!;

    private async Task SendWithBadgesAsync(params RoundBadgeAward[] awards)
    {
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());
        await _handler.Handle(new SendRoundDigestEmailsCommand(7, BadgesAwarded: awards), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldCelebrateABadgeEarnedThisRound()
    {
        GivenDigests(Digest("antony@example.com"));

        await SendWithBadgesAsync(new RoundBadgeAward("u1", BadgeKeys.Sharpshooter1));

        var badges = (System.Collections.IList)Property<object>(CapturedParameters(), "BADGES");
        badges.Count.Should().Be(1);
        Property<string>(badges[0]!, "NAME").Should().Be("Sharpshooter");
        Property<string>(badges[0]!, "ICON_URL").Should().Be($"https://test.local/api/badges/{BadgeKeys.Sharpshooter1}.png");
    }

    [Fact]
    public async Task Handle_ShouldOnlyShowTheHighestLevelReachedInOneGroup()
    {
        // Jumping two levels in a single round earns all three keys, but the email should read as
        // one gold Sharpshooter rather than listing bronze, silver and gold.
        GivenDigests(Digest("antony@example.com"));

        await SendWithBadgesAsync(
            new RoundBadgeAward("u1", BadgeKeys.Sharpshooter1),
            new RoundBadgeAward("u1", BadgeKeys.Sharpshooter2),
            new RoundBadgeAward("u1", BadgeKeys.Sharpshooter3));

        var badges = (System.Collections.IList)Property<object>(CapturedParameters(), "BADGES");
        badges.Count.Should().Be(1);
        Property<string>(badges[0]!, "ICON_URL").Should().Contain(BadgeKeys.Sharpshooter3);
    }

    [Fact]
    public async Task Handle_ShouldListBadgesFromDifferentGroupsByName()
    {
        GivenDigests(Digest("antony@example.com"));

        await SendWithBadgesAsync(
            new RoundBadgeAward("u1", BadgeKeys.Sharpshooter1),
            new RoundBadgeAward("u1", BadgeKeys.Marksman1));

        var badges = (System.Collections.IList)Property<object>(CapturedParameters(), "BADGES");
        badges.Count.Should().Be(2);
        Property<string>(badges[0]!, "NAME").Should().Be("Marksman");
        Property<string>(badges[1]!, "NAME").Should().Be("Sharpshooter");
    }

    [Fact]
    public async Task Handle_ShouldOnlyTellEachPlayerAboutTheirOwnBadges()
    {
        GivenDigests(Digest("antony@example.com"));

        await SendWithBadgesAsync(new RoundBadgeAward("someone-else", BadgeKeys.Sharpshooter1));

        ((System.Collections.IList)Property<object>(CapturedParameters(), "BADGES")).Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreABadgeKeyItDoesNotRecognise()
    {
        // A key retired from the catalogue must not break the whole digest run.
        GivenDigests(Digest("antony@example.com"));

        await SendWithBadgesAsync(new RoundBadgeAward("u1", "not-a-real-badge"));

        ((System.Collections.IList)Property<object>(CapturedParameters(), "BADGES")).Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldOmitTheBadgesSection_OnAnAdminResend()
    {
        // A resend carries no awards, since the badges were earned on the original run.
        GivenDigests(Digest("antony@example.com"));
        _roundRepository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(CompletedRound());

        await _handler.Handle(new SendRoundDigestEmailsCommand(7, BadgesAwarded: null), CancellationToken.None);

        ((System.Collections.IList)Property<object>(CapturedParameters(), "BADGES")).Count.Should().Be(0);
    }
}

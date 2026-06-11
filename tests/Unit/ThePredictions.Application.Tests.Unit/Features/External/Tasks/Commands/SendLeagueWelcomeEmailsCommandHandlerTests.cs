using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.External.Tasks.Commands;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.External.Tasks.Commands;

public class SendLeagueWelcomeEmailsCommandHandlerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILeagueWelcomeNotificationRepository _welcomeRepository = Substitute.For<ILeagueWelcomeNotificationRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 11, 14, 0, 0, DateTimeKind.Utc));

    private const long TemplateId = 13;

    private SendLeagueWelcomeEmailsCommandHandler CreateHandler(long templateId = TemplateId)
    {
        var brevoSettings = Options.Create(new BrevoSettings
        {
            Templates = new TemplateSettings { LeagueWelcome = templateId }
        });
        var siteSettings = Options.Create(new SiteSettings { BaseUrl = "https://dev.thepredictions.co.uk" });

        return new SendLeagueWelcomeEmailsCommandHandler(
            _mediator,
            _emailService,
            _welcomeRepository,
            brevoSettings,
            siteSettings,
            _dateTimeProvider,
            Substitute.For<ILogger<SendLeagueWelcomeEmailsCommandHandler>>());
    }

    private static LeagueWelcomeLeague CreateLeague(params LeagueWelcomeRecipient[] recipients) =>
        new(
            LeagueId: 1,
            LeagueName: "Test League",
            SeasonName: "World Cup 2026",
            HasPrizes: true,
            MemberCount: 22,
            NumberOfRounds: 7,
            NumberOfMonths: 2,
            Prizes: [new LeagueWelcomePrize(Domain.Common.Enumerations.PrizeType.Overall, 1, null, 135m)],
            Boosts: [],
            Recipients: recipients);

    private void ArrangeBatch(params LeagueWelcomeLeague[] leagues)
    {
        _mediator.Send(Arg.Any<GetLeagueWelcomeBatchQuery>(), Arg.Any<CancellationToken>())
            .Returns(leagues.ToList());
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroes_WhenTemplateNotConfigured()
    {
        var handler = CreateHandler(templateId: 0);

        var result = await handler.Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        result.Should().Be(new SendLeagueWelcomeEmailsResult(LeaguesProcessed: 0, EmailsSent: 0));
        await _mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<GetLeagueWelcomeBatchQuery>(), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldUseSevenDayWindow_WhenQueryingDueLeagues()
    {
        ArrangeBatch();

        await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<GetLeagueWelcomeBatchQuery>(q =>
                q.NowUtc == _dateTimeProvider.UtcNow
                && q.WindowStartUtc == _dateTimeProvider.UtcNow.AddDays(-7)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendEmailAndLogNotification_ForEachRecipient()
    {
        ArrangeBatch(CreateLeague(
            new LeagueWelcomeRecipient("user-1", "one@test.com", "One"),
            new LeagueWelcomeRecipient("user-2", "two@test.com", "Two")));

        IEnumerable<LeagueWelcomeNotification>? logged = null;
        await _welcomeRepository.AddNotificationsAsync(
            Arg.Do<IEnumerable<LeagueWelcomeNotification>>(n => logged = n.ToList()),
            Arg.Any<CancellationToken>());

        var result = await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        result.Should().Be(new SendLeagueWelcomeEmailsResult(LeaguesProcessed: 1, EmailsSent: 2));
        await _emailService.Received(1).SendTemplatedEmailAsync("one@test.com", TemplateId, Arg.Any<object>());
        await _emailService.Received(1).SendTemplatedEmailAsync("two@test.com", TemplateId, Arg.Any<object>());

        var notifications = logged!.ToList();
        notifications.Should().HaveCount(2);
        notifications.Should().OnlyContain(n => n.LeagueId == 1);
        notifications.Select(n => n.UserId).Should().BeEquivalentTo("user-1", "user-2");
    }

    [Fact]
    public async Task Handle_ShouldNotLogNotifications_WhenLeagueHasNoRecipients()
    {
        ArrangeBatch(CreateLeague());

        var result = await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        result.Should().Be(new SendLeagueWelcomeEmailsResult(LeaguesProcessed: 1, EmailsSent: 0));
        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
        await _welcomeRepository.DidNotReceiveWithAnyArgs().AddNotificationsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroes_WhenNoLeaguesAreDue()
    {
        ArrangeBatch();

        var result = await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        result.Should().Be(new SendLeagueWelcomeEmailsResult(LeaguesProcessed: 0, EmailsSent: 0));
        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }
}

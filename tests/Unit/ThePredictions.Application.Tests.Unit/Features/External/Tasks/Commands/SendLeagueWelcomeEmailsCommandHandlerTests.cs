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

    private SendLeagueWelcomeEmailsCommandHandler CreateHandler(long templateId = TemplateId, bool templatesConfigured = true)
    {
        var brevoSettings = Options.Create(new BrevoSettings
        {
            Templates = templatesConfigured ? new TemplateSettings { LeagueWelcome = templateId } : null
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
        CreateLeague(boosts: [], recipients: recipients);

    private static LeagueWelcomeLeague CreateLeague(
        IReadOnlyList<LeagueWelcomeBoost> boosts,
        bool hasPrizes = true,
        IReadOnlyList<LeagueWelcomePrize>? prizes = null,
        params LeagueWelcomeRecipient[] recipients) =>
        new(
            LeagueId: 1,
            LeagueName: "Test League",
            SeasonName: "World Cup 2026",
            HasPrizes: hasPrizes,
            MemberCount: 22,
            NumberOfRounds: 7,
            NumberOfMonths: 2,
            Prizes: prizes ?? [new LeagueWelcomePrize(Domain.Common.Enumerations.PrizeType.Overall, 1, null, 135m)],
            Boosts: boosts,
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

    [Fact]
    public async Task Handle_ShouldReturnZeroes_WhenNoTemplatesAreConfiguredAtAll()
    {
        var handler = CreateHandler(templatesConfigured: false);

        var result = await handler.Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        result.Should().Be(new SendLeagueWelcomeEmailsResult(LeaguesProcessed: 0, EmailsSent: 0));
        await _mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<GetLeagueWelcomeBatchQuery>(), CancellationToken.None);
    }

    private object CapturedParameters() =>
        _emailService.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IEmailService.SendTemplatedEmailAsync))
            .GetArguments()[2]!;

    private static T Property<T>(object parameters, string name) =>
        (T)parameters.GetType().GetProperty(name)!.GetValue(parameters)!;

    [Fact]
    public async Task Handle_ShouldDescribeEachBoostTheLeagueHasTurnedOn()
    {
        ArrangeBatch(CreateLeague(
            boosts:
            [
                new LeagueWelcomeBoost("Double Up", "Doubles your points", "/img/double-up.png", 2, []),
                new LeagueWelcomeBoost("Banker", null, null, 1, [])
            ],
            recipients: new LeagueWelcomeRecipient("user-1", "one@test.com", "One")));

        await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        var parameters = CapturedParameters();
        Property<bool>(parameters, "HAS_BOOSTS").Should().BeTrue();
        var boosts = (System.Collections.IList)Property<object>(parameters, "BOOSTS");
        boosts.Count.Should().Be(2);
        Property<string>(boosts[0]!, "BOOST_NAME").Should().Be("Double Up");
        Property<string>(boosts[0]!, "BOOST_DESCRIPTION").Should().Be("Doubles your points");
        Property<string>(boosts[0]!, "BOOST_USAGE").Should().Be("Can be used 2 times this season");
        Property<string>(boosts[1]!, "BOOST_DESCRIPTION").Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldPointBoostImagesAtTheCanonicalSite()
    {
        // Email clients cannot resolve a site-relative path, and the canonical host is used rather
        // than the environment's own so a test-environment email still shows real artwork.
        ArrangeBatch(CreateLeague(
            boosts:
            [
                new LeagueWelcomeBoost("Relative", null, "/img/double-up.png", 1, []),
                new LeagueWelcomeBoost("Absolute", null, "https://cdn.example.com/banker.png", 1, []),
                new LeagueWelcomeBoost("Missing", null, null, 1, [])
            ],
            recipients: new LeagueWelcomeRecipient("user-1", "one@test.com", "One")));

        await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        var boosts = (System.Collections.IList)Property<object>(CapturedParameters(), "BOOSTS");
        Property<string>(boosts[0]!, "BOOST_IMAGE_URL").Should().Be("https://www.thepredictions.co.uk/img/double-up.png");
        Property<string>(boosts[1]!, "BOOST_IMAGE_URL").Should().Be("https://cdn.example.com/banker.png");
        Property<string>(boosts[2]!, "BOOST_IMAGE_URL").Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSayThereAreNoBoosts_WhenTheLeagueHasNoneTurnedOn()
    {
        ArrangeBatch(CreateLeague(new LeagueWelcomeRecipient("user-1", "one@test.com", "One")));

        await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        var parameters = CapturedParameters();
        Property<bool>(parameters, "HAS_BOOSTS").Should().BeFalse();
        ((System.Collections.IList)Property<object>(parameters, "BOOSTS")).Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldSayThereAreNoPrizes_WhenTheLeagueIsNotPlayingForAny()
    {
        ArrangeBatch(CreateLeague(
            boosts: [], hasPrizes: false,
            recipients: new LeagueWelcomeRecipient("user-1", "one@test.com", "One")));

        await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        Property<bool>(CapturedParameters(), "HAS_PRIZES").Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldSayThereAreNoPrizes_WhenTheLeagueWantsThemButNoneAreSetUp()
    {
        // A league flagged as having prizes with nothing actually configured would otherwise render
        // an empty prize section.
        ArrangeBatch(CreateLeague(
            boosts: [], hasPrizes: true, prizes: [],
            recipients: new LeagueWelcomeRecipient("user-1", "one@test.com", "One")));

        await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        Property<bool>(CapturedParameters(), "HAS_PRIZES").Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldLinkToTheLeagueDashboard()
    {
        ArrangeBatch(CreateLeague(new LeagueWelcomeRecipient("user-1", "one@test.com", "One")));

        await CreateHandler().Handle(new SendLeagueWelcomeEmailsCommand(), CancellationToken.None);

        Property<string>(CapturedParameters(), "LEAGUE_URL")
            .Should().Be("https://dev.thepredictions.co.uk/leagues/1/dashboard");
    }
}

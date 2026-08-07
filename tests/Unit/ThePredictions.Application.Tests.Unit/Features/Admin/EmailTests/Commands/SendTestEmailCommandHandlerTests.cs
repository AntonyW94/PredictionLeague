using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.Admin.EmailTests.Commands;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.EmailTests.Commands;

/// <summary>
/// The admin "send me a test of this template" tool. It always sends to the person pressing the
/// button, and reports the provider's answer rather than swallowing it. The round-results digest
/// gets a sample of the repeating sections injected, since the tool can only discover simple tags.
/// </summary>
public class SendTestEmailCommandHandlerTests
{
    private const string CallerUserId = "admin-1";
    private const long DigestTemplateId = 15;
    private const long OtherTemplateId = 9;

    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILogger<SendTestEmailCommandHandler> _logger = Substitute.For<ILogger<SendTestEmailCommandHandler>>();

    private readonly SendTestEmailCommandHandler _handler;

    public SendTestEmailCommandHandlerTests()
    {
        _handler = CreateHandler(new TemplateSettings { RoundResultsDigest = DigestTemplateId });

        _emailService.SendTestTemplatedEmailAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>())
            .Returns(new EmailSendResult(true, "message-123", null));
    }

    private SendTestEmailCommandHandler CreateHandler(TemplateSettings? templates) =>
        new(_userManager, _emailService,
            Options.Create(new BrevoSettings { Templates = templates }),
            Options.Create(new SiteSettings { BaseUrl = "https://test.local/" }),
            _logger);

    private void GivenCaller(string? email = "admin@example.com") =>
        _userManager.FindByIdAsync(CallerUserId).Returns(new ApplicationUser
        {
            Id = CallerUserId,
            Email = email,
            FirstName = "Admin",
            LastName = "User"
        });

    private Task<Contracts.Admin.EmailTests.SendTestEmailResultDto> HandleAsync(
        long templateId = OtherTemplateId, Dictionary<string, string>? parameters = null) =>
        _handler.Handle(
            new SendTestEmailCommand(templateId, parameters ?? new Dictionary<string, string> { ["FIRST_NAME"] = "Admin" }, CallerUserId),
            CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheCallerCannotBeFound()
    {
        var result = await HandleAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Could not determine your email address");
        result.SentTo.Should().BeEmpty();
        await _emailService.DidNotReceiveWithAnyArgs().SendTestTemplatedEmailAsync(default!, default, default!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldRefuse_WhenTheCallerHasNoEmailAddress(string? email)
    {
        GivenCaller(email);

        var result = await HandleAsync();

        result.Success.Should().BeFalse();
        await _emailService.DidNotReceiveWithAnyArgs().SendTestTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task Handle_ShouldSendToWhoeverPressedTheButton()
    {
        GivenCaller();

        var result = await HandleAsync();

        await _emailService.Received(1).SendTestTemplatedEmailAsync(
            "admin@example.com", OtherTemplateId, Arg.Any<object>());
        result.SentTo.Should().Be("admin@example.com");
    }

    [Fact]
    public async Task Handle_ShouldReportTheProvidersAnswerBack()
    {
        GivenCaller();

        var result = await HandleAsync();

        result.Success.Should().BeTrue();
        result.MessageId.Should().Be("message-123");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReportAFailureRatherThanHideIt()
    {
        GivenCaller();
        _emailService.SendTestTemplatedEmailAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>())
            .Returns(new EmailSendResult(false, null, "Template not found"));

        var result = await HandleAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Template not found");
        result.SentTo.Should().Be("admin@example.com");
    }

    [Fact]
    public async Task Handle_ShouldPassTheSuppliedValuesThroughUntouched_ForAnOrdinaryTemplate()
    {
        GivenCaller();
        var parameters = new Dictionary<string, string> { ["FIRST_NAME"] = "Admin" };

        await HandleAsync(OtherTemplateId, parameters);

        await _emailService.Received(1).SendTestTemplatedEmailAsync(
            Arg.Any<string>(), OtherTemplateId, Arg.Is<object>(p => ReferenceEquals(p, parameters)));
    }

    [Fact]
    public async Task Handle_ShouldPassTheSuppliedValuesThroughUntouched_WhenNoTemplatesAreConfigured()
    {
        // With nothing configured there is no digest template to recognise, so nothing is injected.
        GivenCaller();
        var parameters = new Dictionary<string, string> { ["FIRST_NAME"] = "Admin" };

        await CreateHandler(templates: null).Handle(
            new SendTestEmailCommand(DigestTemplateId, parameters, CallerUserId), CancellationToken.None);

        await _emailService.Received(1).SendTestTemplatedEmailAsync(
            Arg.Any<string>(), DigestTemplateId, Arg.Is<object>(p => ReferenceEquals(p, parameters)));
    }

    [Fact]
    public async Task Handle_ShouldAddSampleBadgesAndLeagues_ForTheRoundResultsDigest()
    {
        // The tool only discovers simple tags, so without a sample the digest would preview with
        // its badges and league table sections blank.
        GivenCaller();

        await HandleAsync(DigestTemplateId);

        var sent = CapturedParameters();
        sent.Should().ContainKey("BADGES").And.ContainKey("LEAGUES");
        sent["FIRST_NAME"].Should().Be("Admin");
    }

    [Fact]
    public async Task Handle_ShouldBuildTheSampleLinksFromTheSiteWithoutADoubledSlash()
    {
        GivenCaller();

        await HandleAsync(DigestTemplateId);

        var sent = CapturedParameters();
        var badges = (Array)sent["BADGES"];
        var firstBadgeUrl = (string)badges.GetValue(0)!.GetType().GetProperty("ICON_URL")!.GetValue(badges.GetValue(0))!;
        firstBadgeUrl.Should().Be("https://test.local/api/badges/first-blood.png");

        var leagues = (Array)sent["LEAGUES"];
        var leagueUrl = (string)leagues.GetValue(0)!.GetType().GetProperty("LEAGUE_URL")!.GetValue(leagues.GetValue(0))!;
        leagueUrl.Should().Be("https://test.local/leagues");
    }

    private Dictionary<string, object> CapturedParameters() =>
        (Dictionary<string, object>)_emailService.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IEmailService.SendTestTemplatedEmailAsync))
            .GetArguments()[2]!;
}

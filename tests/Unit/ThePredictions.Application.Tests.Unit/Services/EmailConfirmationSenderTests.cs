using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

/// <summary>
/// Issues the "confirm your email address" link. Registration must never fail because the email
/// could not be delivered, so the token is stored first and every delivery problem is swallowed -
/// the user can always ask for a fresh link.
/// </summary>
public class EmailConfirmationSenderTests
{
    private const long TemplateId = 14;

    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly IEmailConfirmationTokenRepository _tokenRepository = Substitute.For<IEmailConfirmationTokenRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ILogger<EmailConfirmationSender> _logger = Substitute.For<ILogger<EmailConfirmationSender>>();

    private readonly ApplicationUser _user = new()
    {
        Id = "user-1",
        Email = "alice@example.com",
        FirstName = "Alice",
        LastName = "Anderson"
    };

    public EmailConfirmationSenderTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
    }

    private EmailConfirmationSender CreateSender(long templateId = TemplateId, string? baseUrl = "https://test.local")
    {
        var brevo = Options.Create(new BrevoSettings
        {
            Templates = templateId == 0 ? null : new TemplateSettings { EmailConfirmation = templateId }
        });

        return new EmailConfirmationSender(
            _tokenRepository, _emailService, brevo,
            Options.Create(new SiteSettings { BaseUrl = baseUrl }), _dateTimeProvider, _logger);
    }

    private Task SendAsync(long templateId = TemplateId, string? baseUrl = "https://test.local") =>
        CreateSender(templateId, baseUrl).SendAsync(_user, CancellationToken.None);

    [Fact]
    public async Task SendAsync_ShouldReplaceAnyExistingLinkBeforeIssuingANewOne()
    {
        // One live link per person, so an older email cannot also be used.
        await SendAsync();

        Received.InOrder(() =>
        {
            _tokenRepository.DeleteByUserIdAsync("user-1", Arg.Any<CancellationToken>());
            _tokenRepository.CreateAsync(Arg.Any<EmailConfirmationToken>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SendAsync_ShouldStoreATokenThatBelongsToTheUserAndExpires()
    {
        EmailConfirmationToken? stored = null;
        await _tokenRepository.CreateAsync(Arg.Do<EmailConfirmationToken>(t => stored = t), Arg.Any<CancellationToken>());

        await SendAsync();

        stored!.UserId.Should().Be("user-1");
        stored.CreatedAtUtc.Should().Be(NowUtc);
        stored.ExpiresAtUtc.Should().Be(NowUtc.AddHours(72));
    }

    [Fact]
    public async Task SendAsync_ShouldIssueATokenThatIsSafeToPutInALink()
    {
        // The token travels in a query string, so the base64 characters that would need escaping
        // are swapped out and the padding dropped.
        EmailConfirmationToken? stored = null;
        await _tokenRepository.CreateAsync(Arg.Do<EmailConfirmationToken>(t => stored = t), Arg.Any<CancellationToken>());

        await SendAsync();

        stored!.Token.Should().NotBeNullOrWhiteSpace();
        stored.Token.Should().NotContain("+").And.NotContain("/").And.NotEndWith("=");
    }

    [Fact]
    public async Task SendAsync_ShouldIssueADifferentTokenEachTime()
    {
        var issued = new List<string>();
        await _tokenRepository.CreateAsync(Arg.Do<EmailConfirmationToken>(t => issued.Add(t.Token)), Arg.Any<CancellationToken>());

        await SendAsync();
        await SendAsync();

        issued.Should().HaveCount(2);
        issued[0].Should().NotBe(issued[1]);
    }

    [Fact]
    public async Task SendAsync_ShouldEmailTheConfirmationLinkToTheUser()
    {
        EmailConfirmationToken? stored = null;
        await _tokenRepository.CreateAsync(Arg.Do<EmailConfirmationToken>(t => stored = t), Arg.Any<CancellationToken>());

        await SendAsync();

        await _emailService.Received(1).SendTemplatedEmailAsync(
            "alice@example.com",
            TemplateId,
            Arg.Is<object>(p => HasLink(p, $"https://test.local/authentication/confirm-email?token={stored!.Token}")));
    }

    [Fact]
    public async Task SendAsync_ShouldBuildTheLinkFromTheCanonicalSite_WhenNoBaseUrlIsConfigured()
    {
        // The link is never derived from a request header - that would let an attacker point the
        // confirmation at their own site.
        await SendAsync(baseUrl: null);

        await _emailService.Received(1).SendTemplatedEmailAsync(
            Arg.Any<string>(),
            TemplateId,
            Arg.Is<object>(p => StartsWithSite(p, SiteSettings.FallbackBaseUrl)));
    }

    [Fact]
    public async Task SendAsync_ShouldGreetTheUserByName()
    {
        await SendAsync();

        await _emailService.Received(1).SendTemplatedEmailAsync(
            Arg.Any<string>(), TemplateId, Arg.Is<object>(p => FirstNameIs(p, "Alice")));
    }

    [Fact]
    public async Task SendAsync_ShouldStillStoreTheToken_WhenTheTemplateIsNotConfiguredYet()
    {
        // Nothing can be delivered, but the stored token means a later resend works once the
        // template exists.
        await SendAsync(templateId: 0);

        await _tokenRepository.Received(1).CreateAsync(Arg.Any<EmailConfirmationToken>(), Arg.Any<CancellationToken>());
        await _emailService.DidNotReceiveWithAnyArgs().SendTemplatedEmailAsync(default!, default, default!);
    }

    [Fact]
    public async Task SendAsync_ShouldNotThrow_WhenTheEmailCannotBeDelivered()
    {
        // Registration must not fail because the mail provider is down.
        _emailService.SendTemplatedEmailAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<object>())
            .ThrowsAsync(new InvalidOperationException("Brevo unavailable"));

        var act = () => SendAsync();

        await act.Should().NotThrowAsync();
        await _tokenRepository.Received(1).CreateAsync(Arg.Any<EmailConfirmationToken>(), Arg.Any<CancellationToken>());
    }

    private static bool HasLink(object parameters, string expected) =>
        (string)parameters.GetType().GetProperty("CONFIRM_LINK")!.GetValue(parameters)! == expected;

    private static bool StartsWithSite(object parameters, string expected) =>
        ((string)parameters.GetType().GetProperty("CONFIRM_LINK")!.GetValue(parameters)!).StartsWith(expected, StringComparison.Ordinal);

    private static bool FirstNameIs(object parameters, string expected) =>
        (string)parameters.GetType().GetProperty("FIRST_NAME")!.GetValue(parameters)! == expected;
}

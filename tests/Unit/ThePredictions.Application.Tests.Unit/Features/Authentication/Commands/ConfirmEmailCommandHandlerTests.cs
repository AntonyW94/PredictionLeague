using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Models;
using ThePredictions.Application.Features.Authentication.Commands.ConfirmEmail;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

/// <summary>
/// Clicking the "confirm your email address" link. Every way the link can fail gives the same
/// message on purpose, so it cannot be used to work out which addresses are registered.
/// </summary>
public class ConfirmEmailCommandHandlerTests
{
    private const string TokenValue = "token-abc";
    private const string ExpectedMessage = "This confirmation link is invalid or has expired. Please request a new one.";

    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly IEmailConfirmationTokenRepository _tokenRepository = Substitute.For<IEmailConfirmationTokenRepository>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly ConfirmEmailCommandHandler _handler;

    public ConfirmEmailCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _handler = new ConfirmEmailCommandHandler(_tokenRepository, _userManager, _dateTimeProvider);
        _userManager.UpdateAsync(Arg.Any<ApplicationUser>()).Returns(UserManagerResult.Success());
    }

    private void GivenToken(DateTime? expiresAtUtc = null, string userId = "user-1") =>
        _tokenRepository.GetByTokenAsync(TokenValue, Arg.Any<CancellationToken>()).Returns(
            new EmailConfirmationToken(TokenValue, userId, NowUtc.AddHours(-1), expiresAtUtc ?? NowUtc.AddHours(71)));

    private ApplicationUser GivenUser(bool emailConfirmed = false, string userId = "user-1")
    {
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "alice@example.com",
            FirstName = "Alice",
            LastName = "Anderson",
            EmailConfirmed = emailConfirmed
        };

        _userManager.FindByIdAsync(userId).Returns(user);
        return user;
    }

    private Task HandleAsync() => _handler.Handle(new ConfirmEmailCommand(TokenValue), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldRejectALinkThatWasNeverIssued()
    {
        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).WithMessage(ExpectedMessage);
    }

    [Fact]
    public async Task Handle_ShouldRejectALinkThatHasExpired()
    {
        GivenToken(expiresAtUtc: NowUtc.AddSeconds(-1));

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).WithMessage(ExpectedMessage);
    }

    [Fact]
    public async Task Handle_ShouldDiscardAnExpiredLinkSoItCannotBeRetried()
    {
        GivenToken(expiresAtUtc: NowUtc.AddSeconds(-1));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(HandleAsync);

        await _tokenRepository.Received(1).DeleteByUserIdAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRejectALinkWhoseAccountNoLongerExists()
    {
        GivenToken();

        var act = () => HandleAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).WithMessage(ExpectedMessage);
    }

    [Fact]
    public async Task Handle_ShouldConfirmTheAddress()
    {
        GivenToken();
        var user = GivenUser();

        await HandleAsync();

        user.EmailConfirmed.Should().BeTrue();
        await _userManager.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task Handle_ShouldNotRewriteAnAccountThatIsAlreadyConfirmed()
    {
        // Following the same link twice is harmless and must not cost a write.
        GivenToken();
        GivenUser(emailConfirmed: true);

        await HandleAsync();

        await _userManager.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldUseUpTheLinkOnceItHasWorked()
    {
        GivenToken();
        GivenUser();

        await HandleAsync();

        await _tokenRepository.Received(1).DeleteByUserIdAsync("user-1", Arg.Any<CancellationToken>());
    }
}

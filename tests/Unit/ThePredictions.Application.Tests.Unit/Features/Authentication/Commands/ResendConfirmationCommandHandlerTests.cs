using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Features.Authentication.Commands.ResendConfirmation;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

/// <summary>
/// "Send me the confirmation email again". It succeeds silently whatever happens - telling the
/// caller that an account does not exist would let someone probe for registered addresses - and
/// caps the number of requests an hour so it cannot be turned into a way to spam an inbox.
/// </summary>
public class ResendConfirmationCommandHandlerTests
{
    private const string UserId = "user-1";

    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IEmailConfirmationTokenRepository _tokenRepository = Substitute.For<IEmailConfirmationTokenRepository>();
    private readonly IEmailConfirmationSender _sender = Substitute.For<IEmailConfirmationSender>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly ILogger<ResendConfirmationCommandHandler> _logger = Substitute.For<ILogger<ResendConfirmationCommandHandler>>();

    private readonly ResendConfirmationCommandHandler _handler;

    public ResendConfirmationCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _handler = new ResendConfirmationCommandHandler(
            _userManager, _tokenRepository, _sender, _dateTimeProvider, _logger);
    }

    private ApplicationUser GivenUser(bool emailConfirmed = false)
    {
        var user = new ApplicationUser
        {
            Id = UserId,
            Email = "alice@example.com",
            FirstName = "Alice",
            LastName = "Anderson",
            EmailConfirmed = emailConfirmed
        };

        _userManager.FindByIdAsync(UserId).Returns(user);
        return user;
    }

    private void GivenRequestsInTheLastHour(int count) =>
        _tokenRepository.CountByUserIdSinceAsync(UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(count);

    private Task HandleAsync() => _handler.Handle(new ResendConfirmationCommand(UserId), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldDoNothingQuietly_WhenNoSuchAccountExists()
    {
        await HandleAsync();

        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheAddressIsAlreadyConfirmed()
    {
        GivenUser(emailConfirmed: true);

        await HandleAsync();

        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldSendAFreshLink()
    {
        var user = GivenUser();
        GivenRequestsInTheLastHour(0);

        await HandleAsync();

        await _sender.Received(1).SendAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldOnlyCountRequestsFromTheLastHour()
    {
        GivenUser();

        await HandleAsync();

        await _tokenRepository.Received(1).CountByUserIdSinceAsync(
            UserId, NowUtc.AddHours(-1), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Handle_ShouldStopSendingOnceThreeHaveGoneOutThisHour(int alreadySent)
    {
        GivenUser();
        GivenRequestsInTheLastHour(alreadySent);

        await HandleAsync();

        var expected = alreadySent < 3 ? 1 : 0;
        await _sender.Received(expected).SendAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }
}

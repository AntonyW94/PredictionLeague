using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Authentication.Commands.Logout;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Authentication.Commands;

public class LogoutCommandHandlerTests
{
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc));
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(_refreshTokenRepository, _dateTimeProvider);
    }

    private RefreshToken ActiveTokenFor(string userId, string token)
    {
        return new RefreshToken(
            id: 1,
            userId: userId,
            token: token,
            expires: _dateTimeProvider.UtcNow.AddDays(7),
            created: _dateTimeProvider.UtcNow.AddDays(-1),
            revoked: null);
    }

    [Fact]
    public async Task Handle_ShouldRevokeOnlyThePresentedToken_WhenItBelongsToTheUser()
    {
        // Arrange
        var storedToken = ActiveTokenFor("user-1", "device-token");
        _refreshTokenRepository.GetByTokenAsync("device-token", Arg.Any<CancellationToken>()).Returns(storedToken);
        var command = new LogoutCommand("user-1", "device-token");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        storedToken.Revoked.Should().Be(_dateTimeProvider.UtcNow);
        await _refreshTokenRepository.Received(1).UpdateAsync(storedToken, Arg.Any<CancellationToken>());
        await _refreshTokenRepository.DidNotReceiveWithAnyArgs().RevokeAllForUserAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenNoRefreshTokenProvided()
    {
        // Arrange
        var command = new LogoutCommand("user-1");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _refreshTokenRepository.DidNotReceiveWithAnyArgs().GetByTokenAsync(default!, CancellationToken.None);
        await _refreshTokenRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTokenNotFound()
    {
        // Arrange
        _refreshTokenRepository.GetByTokenAsync("missing", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);
        var command = new LogoutCommand("user-1", "missing");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _refreshTokenRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTokenBelongsToAnotherUser()
    {
        // Arrange
        var storedToken = ActiveTokenFor("someone-else", "device-token");
        _refreshTokenRepository.GetByTokenAsync("device-token", Arg.Any<CancellationToken>()).Returns(storedToken);
        var command = new LogoutCommand("user-1", "device-token");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _refreshTokenRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }
}

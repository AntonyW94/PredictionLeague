using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Common.Models;
using ThePredictions.Application.Features.Account.Commands;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Account.Commands;

public class UpdateUserDetailsCommandHandlerTests
{
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IBadgeAwardService _badgeAwardService = Substitute.For<IBadgeAwardService>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly UpdateUserDetailsCommandHandler _handler;

    public UpdateUserDetailsCommandHandlerTests()
    {
        _handler = new UpdateUserDetailsCommandHandler(_userManager, _badgeAwardService, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ShouldUpdateUserDetails_WhenUserExists()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-1",
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = null
        };
        var command = new UpdateUserDetailsCommand("user-1", "Jane", "Smith", "07700900123", MarketingOptIn: false);

        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user).Returns(UserManagerResult.Success());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
        user.PhoneNumber.Should().Be("07700900123");
        await _userManager.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task Handle_ShouldUpdateUserDetails_WhenPhoneNumberIsNull()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-1",
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "07700900123"
        };
        var command = new UpdateUserDetailsCommand("user-1", "Jane", "Smith", null, MarketingOptIn: false);

        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user).Returns(UserManagerResult.Success());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.PhoneNumber.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ShouldSetMarketingOptIn_FromCommand(bool optIn)
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-1", FirstName = "John", LastName = "Doe" };
        var command = new UpdateUserDetailsCommand("user-1", "Jane", "Smith", null, MarketingOptIn: optIn);

        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user).Returns(UserManagerResult.Success());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        if (optIn)
            user.MarketingOptInAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        else
            user.MarketingOptInAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenUserNotFound()
    {
        // Arrange
        var command = new UpdateUserDetailsCommand("unknown-user", "Jane", "Smith", null, MarketingOptIn: false);

        _userManager.FindByIdAsync("unknown-user").Returns((ApplicationUser?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowIdentityUpdateException_WhenUpdateFails()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-1", FirstName = "John", LastName = "Doe" };
        var command = new UpdateUserDetailsCommand("user-1", "Jane", "Smith", null, MarketingOptIn: false);

        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user).Returns(UserManagerResult.Failure(new[] { "Update failed" }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<IdentityUpdateException>();
    }
}

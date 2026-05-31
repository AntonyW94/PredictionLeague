using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

public class SeasonAccessServiceTests
{
    private readonly ISeasonPassRepository _seasonPassRepository = Substitute.For<ISeasonPassRepository>();
    private readonly SeasonAccessService _service;

    private const string UserId = "user-123";
    private const int SeasonId = 7;

    public SeasonAccessServiceTests()
    {
        _service = new SeasonAccessService(_seasonPassRepository);
    }

    [Fact]
    public async Task EnsureCanParticipate_ShouldAllow_WhenPassExists()
    {
        // Arrange
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var act = () => _service.EnsureCanParticipateAsync(UserId, SeasonId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanParticipate_ShouldThrow_WhenNoPassHeld()
    {
        // Arrange — the gate no longer grants a pass; acquisition is a separate explicit action.
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var act = () => _service.EnsureCanParticipateAsync(UserId, SeasonId, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<SeasonPassRequiredException>();
        ex.Which.SeasonId.Should().Be(SeasonId);
        await _seasonPassRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EnsureCanParticipate_ShouldThrow_WhenUserIdMissing(string userId)
    {
        var act = () => _service.EnsureCanParticipateAsync(userId, SeasonId, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EnsureCanParticipate_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => _service.EnsureCanParticipateAsync(UserId, 0, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

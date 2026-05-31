using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.SeasonPasses.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.SeasonPasses;

public class AcquireSeasonPassCommandHandlerTests
{
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly ISeasonPassRepository _seasonPassRepository = Substitute.For<ISeasonPassRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly AcquireSeasonPassCommandHandler _handler;

    private const string UserId = "user-123";
    private const int SeasonId = 7;

    public AcquireSeasonPassCommandHandlerTests()
    {
        _handler = new AcquireSeasonPassCommandHandler(_seasonRepository, _seasonPassRepository, _dateTimeProvider);
    }

    private Season FreeSeason() =>
        new(id: SeasonId, name: "2025/26", startDateUtc: _dateTimeProvider.UtcNow.AddMonths(1),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(7), isActive: true, numberOfRounds: 38,
            competitionId: 1, passStandardPrice: null, passPremiumPrice: null);

    private Season PaidSeason() =>
        new(id: SeasonId, name: "2026/27", startDateUtc: _dateTimeProvider.UtcNow.AddMonths(1),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(7), isActive: true, numberOfRounds: 38,
            competitionId: 1, passStandardPrice: 10m, passPremiumPrice: 15m);

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenPassAlreadyExists()
    {
        // Arrange
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _handler.Handle(new AcquireSeasonPassCommand(UserId, SeasonId), CancellationToken.None);

        // Assert
        await _seasonPassRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
        await _seasonRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldGrantFreePass_WhenSeasonIsFree()
    {
        // Arrange
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(FreeSeason());

        // Act
        await _handler.Handle(new AcquireSeasonPassCommand(UserId, SeasonId), CancellationToken.None);

        // Assert
        await _seasonPassRepository.Received(1).AddAsync(
            Arg.Is<SeasonPass>(p => p.UserId == UserId && p.SeasonId == SeasonId
                && p.Source == SeasonPassSource.Free && p.Tier == SeasonPassTier.Standard && p.AmountPaid == 0m),
            Arg.Any<CancellationToken>());
        await _seasonPassRepository.DidNotReceiveWithAnyArgs().CountForUserAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldGrantTrial_WhenPaidSeasonAndNoPriorRecords()
    {
        // Arrange
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(PaidSeason());
        _seasonPassRepository.CountForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        await _handler.Handle(new AcquireSeasonPassCommand(UserId, SeasonId), CancellationToken.None);

        // Assert
        await _seasonPassRepository.Received(1).AddAsync(
            Arg.Is<SeasonPass>(p => p.UserId == UserId && p.SeasonId == SeasonId
                && p.Source == SeasonPassSource.Trial && p.Tier == SeasonPassTier.Standard && p.AmountPaid == 0m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPaidSeasonAndHasPriorRecords()
    {
        // Arrange — non-trial paid acquisition needs Stripe (Phase B), not this free path.
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(PaidSeason());
        _seasonPassRepository.CountForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns(1);

        // Act
        var act = () => _handler.Handle(new AcquireSeasonPassCommand(UserId, SeasonId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _seasonPassRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFound_WhenSeasonMissing()
    {
        // Arrange
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns((Season?)null);

        // Act
        var act = () => _handler.Handle(new AcquireSeasonPassCommand(UserId, SeasonId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Handle_ShouldThrow_WhenUserIdMissing(string userId)
    {
        var act = () => _handler.Handle(new AcquireSeasonPassCommand(userId, SeasonId), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => _handler.Handle(new AcquireSeasonPassCommand(UserId, 0), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

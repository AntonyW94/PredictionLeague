using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ThePredictions.Application.Features.SeasonPasses.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.SeasonPasses;

public class FulfilSeasonPassCommandHandlerTests
{
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly ISeasonPassRepository _seasonPassRepository = Substitute.For<ISeasonPassRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly FulfilSeasonPassCommandHandler _handler;

    private const string UserId = "user-123";
    private const int SeasonId = 7;
    private const string PaymentReference = "pi_test_123";

    public FulfilSeasonPassCommandHandlerTests()
    {
        _handler = new FulfilSeasonPassCommandHandler(
            _seasonRepository, _seasonPassRepository, _dateTimeProvider, NullLogger<FulfilSeasonPassCommandHandler>.Instance);
    }

    private Season PaidSeason() =>
        new(id: SeasonId, name: "2026/27", startDateUtc: _dateTimeProvider.UtcNow.AddMonths(1),
            endDateUtc: _dateTimeProvider.UtcNow.AddMonths(9), isActive: true, numberOfRounds: 38,
            competitionId: 1, passStandardPrice: 10m, passPremiumPrice: 15m);

    private static FulfilSeasonPassCommand Command(SeasonPassTier tier = SeasonPassTier.Standard, decimal amountPaid = 10m, decimal smsFeePaid = 0m) =>
        new(UserId, SeasonId, tier, amountPaid, smsFeePaid, PaymentReference);

    [Fact]
    public async Task Handle_ShouldCreatePurchasedPass_WhenNoExistingPass()
    {
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(PaidSeason());

        await _handler.Handle(Command(), CancellationToken.None);

        await _seasonPassRepository.Received(1).AddAsync(
            Arg.Is<SeasonPass>(p => p.UserId == UserId && p.SeasonId == SeasonId
                && p.Source == SeasonPassSource.Purchased && p.Tier == SeasonPassTier.Standard
                && p.AmountPaid == 10m && p.SmsFeePaid == 0m && p.StripePaymentReference == PaymentReference),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenPassAlreadyExists()
    {
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(true);

        await _handler.Handle(Command(), CancellationToken.None);

        await _seasonPassRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
        await _seasonRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFound_WhenSeasonMissing()
    {
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns((Season?)null);

        var act = () => _handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
        await _seasonPassRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Handle_ShouldThrow_WhenUserIdMissing(string userId)
    {
        var act = () => _handler.Handle(new FulfilSeasonPassCommand(userId, SeasonId, SeasonPassTier.Standard, 10m, 0m, PaymentReference), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => _handler.Handle(new FulfilSeasonPassCommand(UserId, 0, SeasonPassTier.Standard, 10m, 0m, PaymentReference), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Handle_ShouldThrow_WhenPaymentReferenceMissing(string reference)
    {
        var act = () => _handler.Handle(new FulfilSeasonPassCommand(UserId, SeasonId, SeasonPassTier.Standard, 10m, 0m, reference), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

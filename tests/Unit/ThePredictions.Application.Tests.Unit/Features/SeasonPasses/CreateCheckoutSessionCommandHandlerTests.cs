using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Features.SeasonPasses.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Application.Services.Payments;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.SeasonPasses;

public class CreateCheckoutSessionCommandHandlerTests
{
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly ISeasonPassRepository _seasonPassRepository = Substitute.For<ISeasonPassRepository>();
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly CreateCheckoutSessionCommandHandler _handler;

    private const string UserId = "user-123";
    private const int SeasonId = 7;
    private const string CheckoutUrl = "https://checkout.stripe.com/c/pay/cs_test_123";

    public CreateCheckoutSessionCommandHandlerTests()
    {
        _userManager.FindByIdAsync(UserId).Returns(new ApplicationUser { Id = UserId, EmailConfirmed = true });

        var siteSettings = Options.Create(new SiteSettings { BaseUrl = "https://dev.thepredictions.co.uk" });
        _handler = new CreateCheckoutSessionCommandHandler(_seasonRepository, _seasonPassRepository, _userManager, _paymentService, siteSettings, Substitute.For<ILogger<CreateCheckoutSessionCommandHandler>>());

        _paymentService.CreateCheckoutSessionAsync(Arg.Any<PaymentCheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentCheckoutResult("cs_test_123", CheckoutUrl));
    }

    private static Season PaidSeason(decimal? premiumPrice = 15m) =>
        new(id: SeasonId, name: "2026/27", startDateUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            endDateUtc: new DateTime(2027, 5, 1, 0, 0, 0, DateTimeKind.Utc), isActive: true, numberOfRounds: 38,
            competitionId: 1, passStandardPrice: 10m, passPremiumPrice: premiumPrice);

    private static Season FreeSeason() =>
        new(id: SeasonId, name: "World Cup 2026", startDateUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            endDateUtc: new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc), isActive: true, numberOfRounds: 7,
            competitionId: 2, passStandardPrice: null, passPremiumPrice: null);

    private void ArrangePayableUser(Season? season = null)
    {
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(season ?? PaidSeason());
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);
        _seasonPassRepository.CountForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnCheckoutUrl_WhenUserMustPay()
    {
        ArrangePayableUser();

        var result = await _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);

        result.CheckoutUrl.Should().Be(CheckoutUrl);
    }

    [Fact]
    public async Task Handle_ShouldChargeStandardPriceWithNoSmsFee_ForStandardTier()
    {
        ArrangePayableUser();

        await _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);

        await _paymentService.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<PaymentCheckoutRequest>(r => r.UserId == UserId && r.SeasonId == SeasonId
                && r.Tier == SeasonPassTier.Standard && r.AmountToCharge == 10m && r.SmsFeePaid == 0m
                && r.SuccessUrl.Contains($"/season-passes?seasonId={SeasonId}") && r.SuccessUrl.Contains("{CHECKOUT_SESSION_ID}")
                && r.CancelUrl.Contains("checkout=cancelled")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldChargePremiumPriceWithSmsUplift_ForPremiumTier()
    {
        ArrangePayableUser();

        await _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Premium), CancellationToken.None);

        await _paymentService.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<PaymentCheckoutRequest>(r => r.Tier == SeasonPassTier.Premium && r.AmountToCharge == 15m && r.SmsFeePaid == 5m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenSeasonIsFree()
    {
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(FreeSeason());

        var act = () => _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        await _paymentService.DidNotReceiveWithAnyArgs().CreateCheckoutSessionAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserAlreadyHoldsPass()
    {
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(PaidSeason());
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        await _paymentService.DidNotReceiveWithAnyArgs().CreateCheckoutSessionAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsTrialEligible()
    {
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(PaidSeason());
        _seasonPassRepository.ExistsForUserSeasonAsync(UserId, SeasonId, Arg.Any<CancellationToken>()).Returns(false);
        _seasonPassRepository.CountForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns(0);

        var act = () => _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        await _paymentService.DidNotReceiveWithAnyArgs().CreateCheckoutSessionAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPremiumRequestedButNotOffered()
    {
        ArrangePayableUser(PaidSeason(premiumPrice: null));

        var act = () => _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Premium), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowEmailNotConfirmed_WhenUserEmailUnconfirmed()
    {
        _userManager.FindByIdAsync(UserId).Returns(new ApplicationUser { Id = UserId, EmailConfirmed = false });

        var act = () => _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);

        await act.Should().ThrowAsync<EmailNotConfirmedException>();
        await _paymentService.DidNotReceiveWithAnyArgs().CreateCheckoutSessionAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFound_WhenSeasonMissing()
    {
        _seasonRepository.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns((Season?)null);

        var act = () => _handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Handle_ShouldThrow_WhenUserIdMissing(string userId)
    {
        var act = () => _handler.Handle(new CreateCheckoutSessionCommand(userId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => _handler.Handle(new CreateCheckoutSessionCommand(UserId, 0, SeasonPassTier.Standard), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_ShouldSendTheShopperBackToTheCanonicalSite_WhenNoBaseUrlIsConfigured()
    {
        // Stripe needs an absolute return address. With nothing configured it must fall back to the
        // real site rather than building a relative URL the payment page cannot use.
        ArrangePayableUser();
        var handler = new CreateCheckoutSessionCommandHandler(
            _seasonRepository, _seasonPassRepository, _userManager, _paymentService,
            Options.Create(new SiteSettings { BaseUrl = null }),
            Substitute.For<ILogger<CreateCheckoutSessionCommandHandler>>());

        await handler.Handle(new CreateCheckoutSessionCommand(UserId, SeasonId, SeasonPassTier.Standard), CancellationToken.None);

        await _paymentService.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<PaymentCheckoutRequest>(r =>
                r.SuccessUrl.StartsWith("https://www.thepredictions.co.uk/")
                && r.CancelUrl.StartsWith("https://www.thepredictions.co.uk/")),
            Arg.Any<CancellationToken>());
    }
}

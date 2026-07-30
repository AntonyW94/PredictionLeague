using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Application.Services.Payments;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public class CreateCheckoutSessionCommandHandler(
    ISeasonRepository seasonRepository,
    ISeasonPassRepository seasonPassRepository,
    IUserManager userManager,
    IPaymentService paymentService,
    IOptions<SiteSettings> siteSettings,
    ILogger<CreateCheckoutSessionCommandHandler> logger) : IRequestHandler<CreateCheckoutSessionCommand, CreateCheckoutSessionResponse>
{
    private const string DefaultBaseUrl = "https://www.thepredictions.co.uk";

    private readonly SiteSettings _siteSettings = siteSettings.Value;

    public async Task<CreateCheckoutSessionResponse> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.UserId);
        Guard.Against.NegativeOrZero(request.SeasonId);

        // A confirmed email is required before paying (ADR 0009(b) / 0014), matching the free-acquire gate.
        var user = await userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, nameof(ApplicationUser));

        if (!user!.EmailConfirmed)
            throw new EmailNotConfirmedException();

        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, nameof(Season));

        if (!season!.RequiresPayment)
            throw new BusinessRuleViolationException($"Season (ID: {request.SeasonId}) is free; acquire the pass instead of paying.");

        if (await seasonPassRepository.ExistsForUserSeasonAsync(request.UserId, request.SeasonId, cancellationToken))
            throw new BusinessRuleViolationException($"User (ID: {request.UserId}) already holds a pass for season (ID: {request.SeasonId}).");

        // A user with no prior records is trial-eligible and must never be charged; they acquire a free trial instead.
        var priorRecordCount = await seasonPassRepository.CountForUserAsync(request.UserId, cancellationToken);
        if (priorRecordCount == 0)
            throw new BusinessRuleViolationException($"User (ID: {request.UserId}) is eligible for a free trial; acquire the pass instead of paying.");

        var (amountToCharge, smsFeePaid) = ResolvePricing(season, request.Tier);

        var baseUrl = (string.IsNullOrWhiteSpace(_siteSettings.BaseUrl) ? DefaultBaseUrl : _siteSettings.BaseUrl).TrimEnd('/');
        var successUrl = $"{baseUrl}/season-passes?seasonId={request.SeasonId}&checkout=success&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl}/season-passes?seasonId={request.SeasonId}&checkout=cancelled";

        var result = await paymentService.CreateCheckoutSessionAsync(
            new PaymentCheckoutRequest(request.UserId, request.SeasonId, request.Tier, amountToCharge, smsFeePaid, successUrl, cancelUrl),
            cancellationToken);

        // Money: this is the start of the trail that "I paid and did not get access" is settled
        // against, paired with the fulfilment logs in FulfilSeasonPassCommandHandler.
        logger.LogInformation(
            "Checkout session created for User (ID: {UserId}), Season (ID: {SeasonId}), tier {Tier}, amount {AmountToCharge}.",
            request.UserId,
            request.SeasonId,
            request.Tier,
            amountToCharge);

        return new CreateCheckoutSessionResponse(result.Url);
    }

    private static (decimal AmountToCharge, decimal SmsFeePaid) ResolvePricing(Season season, SeasonPassTier tier)
    {
        // RequiresPayment guarantees a Standard price is set.
        if (tier == SeasonPassTier.Standard)
            return (season.PassStandardPrice!.Value, 0m);

        if (!season.PassPremiumPrice.HasValue)
            throw new BusinessRuleViolationException($"Season (ID: {season.Id}) does not offer a Premium pass.");

        var smsFeePaid = season.PassPremiumPrice.Value - season.PassStandardPrice!.Value;
        return (season.PassPremiumPrice.Value, smsFeePaid);
    }
}

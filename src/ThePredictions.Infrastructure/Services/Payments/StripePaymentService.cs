using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services.Payments;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Infrastructure.Services.Payments;

public class StripePaymentService(IOptions<StripeSettings> settings, ILogger<StripePaymentService> logger) : IPaymentService
{
    private const string PaymentMode = "payment";
    private const string GbpCurrency = "gbp";
    private const string FallbackProductName = "Season Pass";
    private const string CheckoutSessionCompletedEventType = "checkout.session.completed";
    private const string PlaceholderPrefix = "${";

    // Metadata keys carried on the Checkout session so the webhook can fulfil the pass. This service
    // both writes and reads them, so they live here.
    private const string MetaUserId = "userId";
    private const string MetaSeasonId = "seasonId";
    private const string MetaTier = "tier";
    private const string MetaAmountPaid = "amountPaid";
    private const string MetaSmsFeePaid = "smsFeePaid";

    private readonly StripeSettings _settings = settings.Value;

    public async Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
    {
        var options = new SessionCreateOptions
        {
            Mode = PaymentMode,
            ClientReferenceId = request.UserId,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = BuildPriceData(request.AmountToCharge)
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                [MetaUserId] = request.UserId,
                [MetaSeasonId] = request.SeasonId.ToString(CultureInfo.InvariantCulture),
                [MetaTier] = request.Tier.ToString(),
                [MetaAmountPaid] = request.AmountToCharge.ToString(CultureInfo.InvariantCulture),
                [MetaSmsFeePaid] = request.SmsFeePaid.ToString(CultureInfo.InvariantCulture)
            }
        };

        var service = new SessionService(GetClient());
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        logger.LogInformation("Created Stripe checkout session (ID: {SessionId}) for user (ID: {UserId}) in season (ID: {SeasonId}).",
            session.Id, request.UserId, request.SeasonId);

        return new PaymentCheckoutResult(session.Id, session.Url);
    }

    public PaymentCheckoutCompletion? ParseCheckoutCompletedEvent(string requestBody, string signatureHeader)
    {
        if (!IsConfigured(_settings.WebhookSecret))
            throw new InvalidOperationException("Stripe webhook secret is not configured.");

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(requestBody, signatureHeader, _settings.WebhookSecret);
        }
        catch (StripeException ex)
        {
            throw new PaymentWebhookSignatureException(ex.Message);
        }

        if (stripeEvent.Type != CheckoutSessionCompletedEventType)
            return null;

        if (stripeEvent.Data.Object is not Session session)
            return null;

        var metadata = session.Metadata ?? new Dictionary<string, string>();

        var userId = metadata.GetValueOrDefault(MetaUserId) ?? session.ClientReferenceId ?? string.Empty;
        var seasonId = ParseInt(metadata.GetValueOrDefault(MetaSeasonId));
        var tier = ParseTier(metadata.GetValueOrDefault(MetaTier));
        var amountPaid = ParseDecimal(metadata.GetValueOrDefault(MetaAmountPaid));
        var smsFeePaid = ParseDecimal(metadata.GetValueOrDefault(MetaSmsFeePaid));
        var paymentReference = session.PaymentIntentId ?? session.Id;

        return new PaymentCheckoutCompletion(userId, seasonId, tier, amountPaid, smsFeePaid, paymentReference);
    }

    private SessionLineItemPriceDataOptions BuildPriceData(decimal amountToCharge)
    {
        var priceData = new SessionLineItemPriceDataOptions
        {
            Currency = GbpCurrency,
            UnitAmount = ToMinorUnits(amountToCharge)
        };

        // Group sales under the pre-created "Season Pass" product when configured; otherwise fall back
        // to an inline product name so a mis-/un-configured product id never blocks a sale.
        if (IsConfigured(_settings.ProductId))
            priceData.Product = _settings.ProductId;
        else
            priceData.ProductData = new SessionLineItemPriceDataProductDataOptions { Name = FallbackProductName };

        return priceData;
    }

    private StripeClient GetClient()
    {
        if (!IsConfigured(_settings.SecretKey))
            throw new InvalidOperationException("Stripe secret key is not configured.");

        return new StripeClient(_settings.SecretKey);
    }

    private static bool IsConfigured(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.StartsWith(PlaceholderPrefix, StringComparison.Ordinal);

    private static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : 0m;

    private static SeasonPassTier ParseTier(string? value) =>
        Enum.TryParse<SeasonPassTier>(value, out var tier) ? tier : SeasonPassTier.Standard;
}

using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Services.Payments;

public record PaymentCheckoutRequest(
    string UserId,
    int SeasonId,
    SeasonPassTier Tier,
    decimal AmountToCharge,
    decimal SmsFeePaid,
    string SuccessUrl,
    string CancelUrl);

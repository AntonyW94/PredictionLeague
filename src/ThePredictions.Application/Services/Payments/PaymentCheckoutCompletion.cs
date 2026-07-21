using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Services.Payments;

public record PaymentCheckoutCompletion(
    string UserId,
    int SeasonId,
    SeasonPassTier Tier,
    decimal AmountPaid,
    decimal SmsFeePaid,
    string PaymentReference);

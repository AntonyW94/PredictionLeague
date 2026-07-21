namespace ThePredictions.Application.Services.Payments;

public interface IPaymentService
{
    Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the Stripe webhook signature and, for a completed checkout, returns the fulfilment
    /// details. Returns null for any other event type. Throws
    /// <see cref="ThePredictions.Application.Common.Exceptions.PaymentWebhookSignatureException"/>
    /// when the signature cannot be verified.
    /// </summary>
    PaymentCheckoutCompletion? ParseCheckoutCompletedEvent(string requestBody, string signatureHeader);
}

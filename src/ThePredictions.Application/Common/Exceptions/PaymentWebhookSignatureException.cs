namespace ThePredictions.Application.Common.Exceptions;

public class PaymentWebhookSignatureException(string message) : Exception(message);

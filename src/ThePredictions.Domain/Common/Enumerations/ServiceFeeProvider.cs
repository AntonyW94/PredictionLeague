namespace ThePredictions.Domain.Common.Enumerations;

/// <summary>
/// A third party that charges us a per-transaction fee. Stripe takes a percentage + fixed fee on each
/// pass sale; SMS and email providers charge a flat fee per message (percentage 0). Persisted as the
/// enum name.
/// </summary>
public enum ServiceFeeProvider
{
    Stripe,
    Sms,
    Email
}

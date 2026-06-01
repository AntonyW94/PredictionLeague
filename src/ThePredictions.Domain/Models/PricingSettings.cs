using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;

namespace ThePredictions.Domain.Models;

/// <summary>
/// Admin-editable inputs to the recommended-price calculator (ADR 0006): the buffer added on top of
/// costs, the Stripe fee (percentage + fixed), and the minimum price floor. Stored as a single row so
/// the figures can be tuned without a code deploy. Rates are fractions (0.15 = 15%, 0.015 = 1.5%).
/// </summary>
public class PricingSettings
{
    public static readonly decimal DefaultBufferRate = 0.15m;       // +15%
    public static readonly decimal DefaultStripePercent = 0.015m;  // 1.5%
    public static readonly decimal DefaultStripeFixedFee = 0.20m;  // 20p
    public static readonly decimal DefaultMinimumFloor = 1.00m;    // £1

    public int Id { get; init; }
    public decimal BufferRate { get; private set; }
    public decimal StripePercent { get; private set; }
    public decimal StripeFixedFee { get; private set; }
    public decimal MinimumFloor { get; private set; }

    [ExcludeFromCodeCoverage]
    private PricingSettings() { }

    public PricingSettings(int id, decimal bufferRate, decimal stripePercent, decimal stripeFixedFee, decimal minimumFloor)
    {
        Id = id;
        BufferRate = bufferRate;
        StripePercent = stripePercent;
        StripeFixedFee = stripeFixedFee;
        MinimumFloor = minimumFloor;
    }

    /// <summary>The built-in defaults, used to seed the row and as a fallback when none is stored yet.</summary>
    public static PricingSettings CreateDefault() => new()
    {
        BufferRate = DefaultBufferRate,
        StripePercent = DefaultStripePercent,
        StripeFixedFee = DefaultStripeFixedFee,
        MinimumFloor = DefaultMinimumFloor
    };

    public void Update(decimal bufferRate, decimal stripePercent, decimal stripeFixedFee, decimal minimumFloor)
    {
        Validate(bufferRate, stripePercent, stripeFixedFee, minimumFloor);

        BufferRate = bufferRate;
        StripePercent = stripePercent;
        StripeFixedFee = stripeFixedFee;
        MinimumFloor = minimumFloor;
    }

    private static void Validate(decimal bufferRate, decimal stripePercent, decimal stripeFixedFee, decimal minimumFloor)
    {
        Guard.Against.Negative(bufferRate);
        Guard.Against.OutOfRange(stripePercent, nameof(stripePercent), 0m, 0.99m);
        Guard.Against.Negative(stripeFixedFee);
        Guard.Against.Negative(minimumFloor);
    }
}

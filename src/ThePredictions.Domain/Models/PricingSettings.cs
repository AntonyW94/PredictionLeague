using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;

namespace ThePredictions.Domain.Models;

/// <summary>
/// Global, admin-editable knobs for the recommended-price calculator (ADR 0006): the buffer added on
/// top of costs and the minimum price floor. Provider fees (Stripe, SMS, email) live in
/// <see cref="ServiceFee"/>. Stored as a single row so the figures can be tuned without a code deploy.
/// BufferRate is a fraction (0.15 = 15%); MinimumFloor is a GBP amount.
/// </summary>
public class PricingSettings
{
    public static readonly decimal DefaultBufferRate = 0.15m;     // +15%
    public static readonly decimal DefaultMinimumFloor = 1.00m;   // £1

    public int Id { get; init; }
    public decimal BufferRate { get; private set; }
    public decimal MinimumFloor { get; private set; }

    [ExcludeFromCodeCoverage]
    private PricingSettings() { }

    public PricingSettings(int id, decimal bufferRate, decimal minimumFloor)
    {
        Id = id;
        BufferRate = bufferRate;
        MinimumFloor = minimumFloor;
    }

    /// <summary>The built-in defaults, used to seed the row and as a fallback when none is stored yet.</summary>
    public static PricingSettings CreateDefault() => new()
    {
        BufferRate = DefaultBufferRate,
        MinimumFloor = DefaultMinimumFloor
    };

    public void Update(decimal bufferRate, decimal minimumFloor)
    {
        Validate(bufferRate, minimumFloor);

        BufferRate = bufferRate;
        MinimumFloor = minimumFloor;
    }

    private static void Validate(decimal bufferRate, decimal minimumFloor)
    {
        Guard.Against.Negative(bufferRate);
        Guard.Against.Negative(minimumFloor);
    }
}

using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Models;

/// <summary>
/// A per-transaction fee charged by a third party (ADR 0006). Stripe takes a percentage + fixed fee on
/// each sale; SMS/email providers charge a flat fee per message (percentage 0). Stored one row per
/// provider so new providers can be added without a schema change. Percentage is a fraction (0.015 = 1.5%).
/// </summary>
public class ServiceFee
{
    public static readonly decimal DefaultStripePercent = 0.015m;    // 1.5%
    public static readonly decimal DefaultStripeFixedFee = 0.20m;    // 20p

    public int Id { get; init; }
    public ServiceFeeProvider Provider { get; private set; }
    public decimal PercentFee { get; private set; }
    public decimal FixedFee { get; private set; }

    [ExcludeFromCodeCoverage(Justification = "Parameterless constructor for Dapper hydration: no logic to test.")]
    private ServiceFee() { }

    public ServiceFee(int id, ServiceFeeProvider provider, decimal percentFee, decimal fixedFee)
    {
        Id = id;
        Provider = provider;
        PercentFee = percentFee;
        FixedFee = fixedFee;
    }

    /// <summary>Built-in default fee for a provider; used to seed rows and as a fallback when none is stored.</summary>
    public static ServiceFee CreateDefault(ServiceFeeProvider provider)
    {
        var (percentFee, fixedFee) = provider switch
        {
            ServiceFeeProvider.Stripe => (DefaultStripePercent, DefaultStripeFixedFee),
            _ => (0m, 0m)
        };

        return new ServiceFee
        {
            Provider = provider,
            PercentFee = percentFee,
            FixedFee = fixedFee
        };
    }

    public void Update(decimal percentFee, decimal fixedFee)
    {
        Validate(percentFee, fixedFee);

        PercentFee = percentFee;
        FixedFee = fixedFee;
    }

    private static void Validate(decimal percentFee, decimal fixedFee)
    {
        Guard.Against.OutOfRange(percentFee, nameof(percentFee), 0m, 0.99m);
        Guard.Against.Negative(fixedFee);
    }
}

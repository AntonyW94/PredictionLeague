using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Models;

public class SeasonPass
{
    public int Id { get; init; }
    public string UserId { get; private set; } = string.Empty;
    public int SeasonId { get; private set; }
    public SeasonPassTier Tier { get; private set; }
    public SeasonPassSource Source { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal SmsFeePaid { get; private set; }
    public string? StripePaymentReference { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int SmsSentCount { get; private set; }
    public int? RewardRedeemedForSeasonId { get; private set; }

    public bool HasSmsReminders => Tier >= SeasonPassTier.Premium;

    [ExcludeFromCodeCoverage(Justification = "Parameterless constructor for Dapper hydration: no logic to test.")]
    private SeasonPass() { }

    public SeasonPass(int id, string userId, int seasonId, SeasonPassTier tier, SeasonPassSource source,
        decimal amountPaid, decimal smsFeePaid, string? stripePaymentReference, DateTime createdAtUtc,
        int smsSentCount, int? rewardRedeemedForSeasonId)
    {
        Id = id;
        UserId = userId;
        SeasonId = seasonId;
        Tier = tier;
        Source = source;
        AmountPaid = amountPaid;
        SmsFeePaid = smsFeePaid;
        StripePaymentReference = stripePaymentReference;
        CreatedAtUtc = createdAtUtc;
        SmsSentCount = smsSentCount;
        RewardRedeemedForSeasonId = rewardRedeemedForSeasonId;
    }

    public static SeasonPass CreatePurchased(string userId, int seasonId, SeasonPassTier tier,
        decimal amountPaid, decimal smsFeePaid, string stripePaymentReference, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(seasonId);
        Guard.Against.NegativeOrZero(amountPaid);
        Guard.Against.NullOrWhiteSpace(stripePaymentReference);
        Guard.Against.Negative(smsFeePaid);

        if (tier == SeasonPassTier.Standard && smsFeePaid > 0)
            throw new ArgumentException("A Standard-tier pass cannot have an SMS fee.", nameof(smsFeePaid));

        return new SeasonPass
        {
            UserId = userId,
            SeasonId = seasonId,
            Tier = tier,
            Source = SeasonPassSource.Purchased,
            AmountPaid = amountPaid,
            SmsFeePaid = smsFeePaid,
            StripePaymentReference = stripePaymentReference,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };
    }

    // Comped SMS upgrade earned via the early-bird reward: Premium tier, SMS fee 0.
    public static SeasonPass CreateRewardUpgrade(string userId, int seasonId,
        decimal amountPaid, string stripePaymentReference, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(seasonId);
        Guard.Against.NegativeOrZero(amountPaid);
        Guard.Against.NullOrWhiteSpace(stripePaymentReference);

        return new SeasonPass
        {
            UserId = userId,
            SeasonId = seasonId,
            Tier = SeasonPassTier.Premium,
            Source = SeasonPassSource.Purchased,
            AmountPaid = amountPaid,
            SmsFeePaid = 0m,
            StripePaymentReference = stripePaymentReference,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };
    }

    // Free first-season trial: Standard comped, no payment.
    public static SeasonPass CreateTrial(string userId, int seasonId, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(seasonId);

        return new SeasonPass
        {
            UserId = userId,
            SeasonId = seasonId,
            Tier = SeasonPassTier.Standard,
            Source = SeasonPassSource.Trial,
            AmountPaid = 0m,
            SmsFeePaid = 0m,
            StripePaymentReference = null,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };
    }

    // Free first-season trial where the user pays only the SMS uplift on top (Standard comped).
    public static SeasonPass CreateTrialWithSms(string userId, int seasonId, decimal smsFeePaid,
        string stripePaymentReference, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(seasonId);
        Guard.Against.NegativeOrZero(smsFeePaid);
        Guard.Against.NullOrWhiteSpace(stripePaymentReference);

        return new SeasonPass
        {
            UserId = userId,
            SeasonId = seasonId,
            Tier = SeasonPassTier.Premium,
            Source = SeasonPassSource.Trial,
            AmountPaid = smsFeePaid,
            SmsFeePaid = smsFeePaid,
            StripePaymentReference = stripePaymentReference,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };
    }

    // Free-season participation record: £0, Standard tier — exists so free play burns the free-first-season (ADR 0005).
    public static SeasonPass CreateFree(string userId, int seasonId, IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NegativeOrZero(seasonId);

        return new SeasonPass
        {
            UserId = userId,
            SeasonId = seasonId,
            Tier = SeasonPassTier.Standard,
            Source = SeasonPassSource.Free,
            AmountPaid = 0m,
            SmsFeePaid = 0m,
            StripePaymentReference = null,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };
    }

    public void RecordSmsSent()
    {
        SmsSentCount++;
    }

    public void MarkRewardRedeemed(int redeemedForSeasonId)
    {
        Guard.Against.NegativeOrZero(redeemedForSeasonId);
        RewardRedeemedForSeasonId = redeemedForSeasonId;
    }
}

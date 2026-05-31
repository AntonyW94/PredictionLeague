using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class SeasonPassTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    #region CreatePurchased

    [Fact]
    public void CreatePurchased_ShouldSetProperties_WhenStandardTier()
    {
        // Act
        var pass = SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Standard, 10m, 0m, "pi_123", _dateTimeProvider);

        // Assert
        pass.UserId.Should().Be("user-1");
        pass.SeasonId.Should().Be(2);
        pass.Tier.Should().Be(SeasonPassTier.Standard);
        pass.Source.Should().Be(SeasonPassSource.Purchased);
        pass.AmountPaid.Should().Be(10m);
        pass.SmsFeePaid.Should().Be(0m);
        pass.StripePaymentReference.Should().Be("pi_123");
        pass.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        pass.SmsSentCount.Should().Be(0);
    }

    [Fact]
    public void CreatePurchased_ShouldAllowSmsFee_WhenPremiumTier()
    {
        // Act
        var pass = SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Premium, 15m, 5m, "pi_123", _dateTimeProvider);

        // Assert
        pass.Tier.Should().Be(SeasonPassTier.Premium);
        pass.SmsFeePaid.Should().Be(5m);
    }

    [Fact]
    public void CreatePurchased_ShouldThrow_WhenUserIdMissing()
    {
        var act = () => SeasonPass.CreatePurchased(" ", 2, SeasonPassTier.Standard, 10m, 0m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePurchased_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => SeasonPass.CreatePurchased("user-1", 0, SeasonPassTier.Standard, 10m, 0m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePurchased_ShouldThrow_WhenAmountPaidNotPositive()
    {
        var act = () => SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Standard, 0m, 0m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePurchased_ShouldThrow_WhenReferenceIsBlank()
    {
        var act = () => SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Standard, 10m, 0m, " ", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePurchased_ShouldThrow_WhenSmsFeeNegative()
    {
        var act = () => SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Premium, 15m, -1m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePurchased_ShouldThrow_WhenStandardTierHasSmsFee()
    {
        var act = () => SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Standard, 10m, 5m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateRewardUpgrade

    [Fact]
    public void CreateRewardUpgrade_ShouldBePremiumWithNoSmsFee()
    {
        // Act
        var pass = SeasonPass.CreateRewardUpgrade("user-1", 2, 10m, "pi_123", _dateTimeProvider);

        // Assert
        pass.Tier.Should().Be(SeasonPassTier.Premium);
        pass.Source.Should().Be(SeasonPassSource.Purchased);
        pass.AmountPaid.Should().Be(10m);
        pass.SmsFeePaid.Should().Be(0m);
        pass.HasSmsReminders.Should().BeTrue();
    }

    [Fact]
    public void CreateRewardUpgrade_ShouldThrow_WhenUserIdMissing()
    {
        var act = () => SeasonPass.CreateRewardUpgrade(" ", 2, 10m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateRewardUpgrade_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => SeasonPass.CreateRewardUpgrade("user-1", 0, 10m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateRewardUpgrade_ShouldThrow_WhenAmountPaidNotPositive()
    {
        var act = () => SeasonPass.CreateRewardUpgrade("user-1", 2, 0m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateRewardUpgrade_ShouldThrow_WhenReferenceIsBlank()
    {
        var act = () => SeasonPass.CreateRewardUpgrade("user-1", 2, 10m, "", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateTrial

    [Fact]
    public void CreateTrial_ShouldBeFreeStandardTrial()
    {
        // Act
        var pass = SeasonPass.CreateTrial("user-1", 2, _dateTimeProvider);

        // Assert
        pass.Tier.Should().Be(SeasonPassTier.Standard);
        pass.Source.Should().Be(SeasonPassSource.Trial);
        pass.AmountPaid.Should().Be(0m);
        pass.SmsFeePaid.Should().Be(0m);
        pass.StripePaymentReference.Should().BeNull();
    }

    [Fact]
    public void CreateTrial_ShouldThrow_WhenUserIdMissing()
    {
        var act = () => SeasonPass.CreateTrial(" ", 2, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateTrial_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => SeasonPass.CreateTrial("user-1", 0, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateTrialWithSms

    [Fact]
    public void CreateTrialWithSms_ShouldCompStandardAndChargeSmsOnly()
    {
        // Act
        var pass = SeasonPass.CreateTrialWithSms("user-1", 2, 5m, "pi_123", _dateTimeProvider);

        // Assert
        pass.Tier.Should().Be(SeasonPassTier.Premium);
        pass.Source.Should().Be(SeasonPassSource.Trial);
        pass.AmountPaid.Should().Be(5m);
        pass.SmsFeePaid.Should().Be(5m);
        pass.StripePaymentReference.Should().Be("pi_123");
    }

    [Fact]
    public void CreateTrialWithSms_ShouldThrow_WhenUserIdMissing()
    {
        var act = () => SeasonPass.CreateTrialWithSms(" ", 2, 5m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateTrialWithSms_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => SeasonPass.CreateTrialWithSms("user-1", 0, 5m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateTrialWithSms_ShouldThrow_WhenSmsFeeNotPositive()
    {
        var act = () => SeasonPass.CreateTrialWithSms("user-1", 2, 0m, "pi_123", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateTrialWithSms_ShouldThrow_WhenReferenceIsBlank()
    {
        var act = () => SeasonPass.CreateTrialWithSms("user-1", 2, 5m, " ", _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region CreateFree

    [Fact]
    public void CreateFree_ShouldBeZeroStandardFreeRecord()
    {
        // Act
        var pass = SeasonPass.CreateFree("user-1", 2, _dateTimeProvider);

        // Assert
        pass.Tier.Should().Be(SeasonPassTier.Standard);
        pass.Source.Should().Be(SeasonPassSource.Free);
        pass.AmountPaid.Should().Be(0m);
        pass.SmsFeePaid.Should().Be(0m);
        pass.StripePaymentReference.Should().BeNull();
    }

    [Fact]
    public void CreateFree_ShouldThrow_WhenUserIdMissing()
    {
        var act = () => SeasonPass.CreateFree(" ", 2, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFree_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var act = () => SeasonPass.CreateFree("user-1", 0, _dateTimeProvider);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region SMS behaviour

    [Fact]
    public void HasSmsReminders_ShouldBeFalse_WhenStandardTier()
    {
        var pass = SeasonPass.CreateFree("user-1", 2, _dateTimeProvider);
        pass.HasSmsReminders.Should().BeFalse();
    }

    [Fact]
    public void HasSmsReminders_ShouldBeTrue_WhenPremiumTier()
    {
        var pass = SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Premium, 15m, 5m, "pi_123", _dateTimeProvider);
        pass.HasSmsReminders.Should().BeTrue();
    }

    [Fact]
    public void RecordSmsSent_ShouldIncrementCount()
    {
        var pass = SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Premium, 15m, 5m, "pi_123", _dateTimeProvider);

        pass.RecordSmsSent();
        pass.RecordSmsSent();

        pass.SmsSentCount.Should().Be(2);
    }

    #endregion

    #region MarkRewardRedeemed

    [Fact]
    public void MarkRewardRedeemed_ShouldStampSeasonId()
    {
        var pass = SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Premium, 15m, 5m, "pi_123", _dateTimeProvider);

        pass.MarkRewardRedeemed(99);

        pass.RewardRedeemedForSeasonId.Should().Be(99);
    }

    [Fact]
    public void MarkRewardRedeemed_ShouldThrow_WhenSeasonIdNotPositive()
    {
        var pass = SeasonPass.CreatePurchased("user-1", 2, SeasonPassTier.Premium, 15m, 5m, "pi_123", _dateTimeProvider);

        var act = () => pass.MarkRewardRedeemed(0);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Hydration constructor

    [Fact]
    public void Constructor_ShouldHydrateAllProperties()
    {
        // Arrange
        var createdAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var pass = new SeasonPass(id: 5, userId: "user-1", seasonId: 2, tier: SeasonPassTier.Premium,
            source: SeasonPassSource.Purchased, amountPaid: 15m, smsFeePaid: 5m, stripePaymentReference: "pi_123",
            createdAtUtc: createdAt, smsSentCount: 3, rewardRedeemedForSeasonId: 7);

        // Assert
        pass.Id.Should().Be(5);
        pass.UserId.Should().Be("user-1");
        pass.SeasonId.Should().Be(2);
        pass.Tier.Should().Be(SeasonPassTier.Premium);
        pass.Source.Should().Be(SeasonPassSource.Purchased);
        pass.AmountPaid.Should().Be(15m);
        pass.SmsFeePaid.Should().Be(5m);
        pass.StripePaymentReference.Should().Be("pi_123");
        pass.CreatedAtUtc.Should().Be(createdAt);
        pass.SmsSentCount.Should().Be(3);
        pass.RewardRedeemedForSeasonId.Should().Be(7);
        pass.HasSmsReminders.Should().BeTrue();
    }

    #endregion
}

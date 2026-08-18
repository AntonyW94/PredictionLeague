using FluentAssertions;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Contracts.Tests.Unit.Admin.Users;

public class UserSeasonPassDtoTests
{
    private static UserSeasonPassDto Pass(
        SeasonPassSource source = SeasonPassSource.Purchased,
        decimal amountPaid = 0m,
        decimal smsFeePaid = 0m) =>
        new(3, "Premier League 2026/27", true, SeasonPassTier.Standard, source, amountPaid, smsFeePaid, DateTime.UnixEpoch);

    [Fact]
    public void TotalPaid_ShouldAddTheTextMessageFeeToThePassPrice()
    {
        // Two columns because the uplift is priced separately, but nobody asking what a pass cost wants them apart.
        Pass(amountPaid: 10m, smsFeePaid: 2.50m).TotalPaid.Should().Be(12.50m);
    }

    [Fact]
    public void TotalPaid_ShouldBeThePassPrice_WhenNoTextMessageFeeWasPaid()
    {
        Pass(amountPaid: 10m).TotalPaid.Should().Be(10m);
    }

    [Fact]
    public void TotalPaid_ShouldBeZero_ForAPassNobodyPaidFor()
    {
        Pass(SeasonPassSource.Trial).TotalPaid.Should().Be(0m);
    }

    [Fact]
    public void WasPurchased_ShouldBeTrue_ForABoughtPass()
    {
        Pass(SeasonPassSource.Purchased).WasPurchased.Should().BeTrue();
    }

    [Fact]
    public void WasPurchased_ShouldBeFalse_ForATrial()
    {
        // A trial is a pass, and is why the account can play, but nobody paid for it.
        Pass(SeasonPassSource.Trial).WasPurchased.Should().BeFalse();
    }

    [Fact]
    public void WasPurchased_ShouldBeFalse_ForAFreeSeasonPass()
    {
        Pass(SeasonPassSource.Free).WasPurchased.Should().BeFalse();
    }

    [Fact]
    public void WasPurchased_ShouldBeTrue_EvenForAPassThatCostNothing()
    {
        // A comped purchase is still a purchase. Whether money changed hands is TotalPaid's question, not this one, and
        // conflating them would let a £0 purchase disappear from "has ever paid".
        Pass(SeasonPassSource.Purchased, amountPaid: 0m).WasPurchased.Should().BeTrue();
    }
}

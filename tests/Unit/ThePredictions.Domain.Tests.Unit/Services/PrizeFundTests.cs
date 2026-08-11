using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// What a league's prize pot is worth - every member's entry fee plus whatever the administrator has added on top,
/// written out in SQL in two places before this.
/// </summary>
public class PrizeFundTests
{
    [Fact]
    public void Total_ShouldBeTheEntryFeesAddedUp()
    {
        PrizeFund.Total(entryFee: 10m, memberCount: 12, administratorTopUp: null).Should().Be(120m);
    }

    [Fact]
    public void Total_ShouldIncludeTheAdministratorsTopUp()
    {
        PrizeFund.Total(entryFee: 10m, memberCount: 12, administratorTopUp: 50m).Should().Be(170m);
    }

    [Fact]
    public void Total_ShouldBeNothing_ForAFreeLeagueWithNoTopUp()
    {
        // The intended behaviour rather than a special case: a free league has no pot unless it is funded.
        PrizeFund.Total(entryFee: 0m, memberCount: 20, administratorTopUp: null).Should().Be(0m);
    }

    [Fact]
    public void Total_ShouldBeJustTheTopUp_ForAFreeFundedLeague()
    {
        PrizeFund.Total(entryFee: 0m, memberCount: 20, administratorTopUp: 100m).Should().Be(100m);
    }

    [Fact]
    public void Total_ShouldBeNothing_ForALeagueWithNoMembers()
    {
        PrizeFund.Total(entryFee: 10m, memberCount: 0, administratorTopUp: null).Should().Be(0m);
    }

    [Fact]
    public void Remaining_ShouldSubtractWhatHasBeenPaidOut()
    {
        PrizeFund.Remaining(total: 170m, alreadyPaidOut: 70m).Should().Be(100m);
    }

    [Fact]
    public void Remaining_ShouldGoNegative_WhenMoreHasBeenPaidOutThanThePotHolds()
    {
        // Reported rather than clamped: an overpayment is worth seeing, not hiding behind a zero.
        PrizeFund.Remaining(total: 100m, alreadyPaidOut: 150m).Should().Be(-50m);
    }
}

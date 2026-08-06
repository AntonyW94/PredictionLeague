using FluentAssertions;
using ThePredictions.Contracts.Admin.Users;
using Xunit;

namespace ThePredictions.Contracts.Tests.Unit.Admin.Users;

public class UserDtoTests
{
    private static UserDto User(
        bool hasSeasonPass = false,
        int leaguesCreated = 0,
        int leaguesJoinedApproved = 0,
        int leaguesJoinedPending = 0,
        decimal seasonPassSpend = 0m,
        decimal leagueEntrySpend = 0m) =>
        new("user-1", "Alex Player", "alex@example.com", null, false, true, [], true,
            hasSeasonPass, leaguesCreated, leaguesJoinedApproved, leaguesJoinedPending,
            0m, seasonPassSpend, leagueEntrySpend);

    [Fact]
    public void TotalSpend_ShouldAddPassSpendToLeagueEntrySpend()
    {
        User(seasonPassSpend: 12.50m, leagueEntrySpend: 30m).TotalSpend.Should().Be(42.50m);
    }

    [Fact]
    public void TotalSpend_ShouldBeZero_ForAUserWhoHasSpentNothing()
    {
        User().TotalSpend.Should().Be(0m);
    }

    [Fact]
    public void IsDormant_ShouldBeTrue_ForAUserWithNoPassAndNoLeagues()
    {
        User().IsDormant.Should().BeTrue();
    }

    [Fact]
    public void IsDormant_ShouldBeFalse_WhenTheUserHasASeasonPass()
    {
        User(hasSeasonPass: true).IsDormant.Should().BeFalse();
    }

    [Fact]
    public void IsDormant_ShouldBeFalse_WhenTheUserHasCreatedALeague()
    {
        User(leaguesCreated: 1).IsDormant.Should().BeFalse();
    }

    [Fact]
    public void IsDormant_ShouldBeFalse_WhenTheUserHasJoinedALeague()
    {
        User(leaguesJoinedApproved: 1).IsDormant.Should().BeFalse();
    }

    [Fact]
    public void IsDormant_ShouldBeFalse_WhenTheUserHasAPendingRequest()
    {
        // A pending request is still an attempt to take part, so it must not read as dormant.
        User(leaguesJoinedPending: 1).IsDormant.Should().BeFalse();
    }
}

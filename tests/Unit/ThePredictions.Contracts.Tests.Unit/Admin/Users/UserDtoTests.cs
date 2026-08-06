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
        decimal leagueEntrySpend = 0m,
        List<string>? socialProviders = null) =>
        new("user-1", "Alex Player", "alex@example.com", null, false, true, socialProviders ?? [], true,
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

    [Fact]
    public void TwoUsersSharingEveryValueShouldBeEqual()
    {
        // Note the shared SocialProviders instance. A record compares collection members by
        // reference, so two separately-built lists would make otherwise identical users unequal.
        var socialProviders = new List<string> { "Google" };

        var first = User(socialProviders: socialProviders);
        var second = User(socialProviders: socialProviders);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void UsersWithSeparateButEquivalentListsShouldNotBeEqual()
    {
        // Documents the trap above: equal contents are not enough.
        User(socialProviders: ["Google"]).Should().NotBe(User(socialProviders: ["Google"]));
    }

    [Fact]
    public void UsersDifferingInAnyFieldShouldNotBeEqual()
    {
        User(leaguesCreated: 1).Should().NotBe(User(leaguesCreated: 2));
    }

    [Fact]
    public void WithShouldCopyTheUserAndChangeOnlyTheNamedField()
    {
        var original = User(seasonPassSpend: 10m);

        var copy = original with { SeasonPassSpend = 25m };

        copy.SeasonPassSpend.Should().Be(25m);
        copy.Email.Should().Be(original.Email);
        copy.Should().NotBe(original);
    }

    [Fact]
    public void ToStringShouldIncludeTheIdentifyingFields()
    {
        User().ToString().Should().Contain("user-1").And.Contain("alex@example.com");
    }
}

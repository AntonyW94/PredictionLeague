using FluentAssertions;
using ThePredictions.Contracts.Admin.Users;
using Xunit;

namespace ThePredictions.Contracts.Tests.Unit.Admin.Users;

public class UserDeletionImpactDtoTests
{
    private static UserDeletionImpactDto Impact(
        int seasonPasses = 0,
        decimal seasonPassSpend = 0m,
        int leagueMemberships = 0,
        int predictions = 0,
        int winnings = 0,
        decimal winningsTotal = 0m,
        int payouts = 0,
        decimal payoutsTotal = 0m,
        int badges = 0,
        int boostUsages = 0,
        int roundResults = 0,
        int leagueRoundResults = 0,
        int leagueStandings = 0,
        int emailRecords = 0,
        int onboardingSkips = 0,
        bool hasPayoutDetails = false,
        int leaguesAdministered = 0) =>
        new(seasonPasses, seasonPassSpend, leagueMemberships, predictions, winnings, winningsTotal, payouts,
            payoutsTotal, badges, boostUsages, roundResults, leagueRoundResults, leagueStandings, emailRecords,
            onboardingSkips, hasPayoutDetails, leaguesAdministered);

    [Fact]
    public void HasAnyRecords_ShouldBeFalse_ForAnAccountThatHasOnlyEverRegistered()
    {
        Impact().HasAnyRecords.Should().BeFalse();
    }

    [Theory]
    [InlineData(nameof(UserDeletionImpactDto.SeasonPasses))]
    [InlineData(nameof(UserDeletionImpactDto.LeagueMemberships))]
    [InlineData(nameof(UserDeletionImpactDto.Predictions))]
    [InlineData(nameof(UserDeletionImpactDto.Winnings))]
    [InlineData(nameof(UserDeletionImpactDto.Payouts))]
    [InlineData(nameof(UserDeletionImpactDto.Badges))]
    [InlineData(nameof(UserDeletionImpactDto.BoostUsages))]
    [InlineData(nameof(UserDeletionImpactDto.RoundResults))]
    [InlineData(nameof(UserDeletionImpactDto.LeagueRoundResults))]
    [InlineData(nameof(UserDeletionImpactDto.LeagueStandings))]
    [InlineData(nameof(UserDeletionImpactDto.EmailRecords))]
    [InlineData(nameof(UserDeletionImpactDto.OnboardingSkips))]
    public void HasAnyRecords_ShouldBeTrue_WhenAnySingleCategoryHasARow(string category)
    {
        // One case per category rather than one combined case. The property is a chain of twelve ORs, and a
        // single "everything at once" test passes just as happily when one of them was mistyped.
        WithOne(category).HasAnyRecords.Should().BeTrue($"{category} is a record the delete would destroy.");
    }

    [Fact]
    public void HasAnyRecords_ShouldBeTrue_WhenOnlyBankDetailsAreStored()
    {
        Impact(hasPayoutDetails: true).HasAnyRecords.Should().BeTrue();
    }

    [Fact]
    public void TotalRecords_ShouldAddUpEveryCategory()
    {
        // Distinct values so a category added twice, or omitted, changes the total.
        Impact(
            seasonPasses: 1,
            leagueMemberships: 2,
            predictions: 4,
            winnings: 8,
            payouts: 16,
            badges: 32,
            boostUsages: 64,
            roundResults: 128,
            leagueRoundResults: 256,
            leagueStandings: 512,
            emailRecords: 1024,
            onboardingSkips: 2048)
            .TotalRecords.Should().Be(4095);
    }

    [Fact]
    public void TotalRecords_ShouldBeZero_ForAnAccountWithNoHistory()
    {
        Impact().TotalRecords.Should().Be(0);
    }

    [Fact]
    public void TotalRecords_ShouldIgnoreBankDetailsAndAdministeredLeagues()
    {
        // Neither is a countable record of the user's own: the bank details are one row reported as a flag,
        // and the leagues survive the deletion.
        Impact(hasPayoutDetails: true, leaguesAdministered: 4).TotalRecords.Should().Be(0);
    }

    [Fact]
    public void HasAnyRecords_ShouldIgnoreAdministeredLeagues()
    {
        // Those leagues are not deleted - they are handed to somebody else - so an account whose only
        // connection to the site is administering one still has nothing of its own to destroy.
        Impact(leaguesAdministered: 3).HasAnyRecords.Should().BeFalse();
    }

    [Fact]
    public void HasFinancialRecords_ShouldBeFalse_WhenOnlyPlayingHistoryExists()
    {
        Impact(predictions: 412, badges: 6, leagueMemberships: 2).HasFinancialRecords.Should().BeFalse();
    }

    [Theory]
    [InlineData(nameof(UserDeletionImpactDto.SeasonPasses))]
    [InlineData(nameof(UserDeletionImpactDto.Winnings))]
    [InlineData(nameof(UserDeletionImpactDto.Payouts))]
    public void HasFinancialRecords_ShouldBeTrue_WhenMoneyIsInvolved(string category)
    {
        WithOne(category).HasFinancialRecords.Should().BeTrue();
    }

    [Fact]
    public void HasFinancialRecords_ShouldBeTrue_WhenBankDetailsAreStored()
    {
        Impact(hasPayoutDetails: true).HasFinancialRecords.Should().BeTrue();
    }

    [Fact]
    public void TwoImpactsSharingEveryValueShouldBeEqual()
    {
        var first = Impact(seasonPasses: 1, seasonPassSpend: 25m);
        var second = Impact(seasonPasses: 1, seasonPassSpend: 25m);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void ImpactsDifferingInAnyFieldShouldNotBeEqual()
    {
        Impact(predictions: 1).Should().NotBe(Impact(predictions: 2));
    }

    [Fact]
    public void WithShouldCopyTheImpactAndChangeOnlyTheNamedField()
    {
        var original = Impact(seasonPasses: 1, predictions: 10);

        var copy = original with { Predictions = 20 };

        copy.Predictions.Should().Be(20);
        copy.SeasonPasses.Should().Be(original.SeasonPasses);
        copy.Should().NotBe(original);
    }

    [Fact]
    public void ToStringShouldIncludeTheCounts()
    {
        Impact(seasonPasses: 1, predictions: 412).ToString()
            .Should().Contain("SeasonPasses = 1").And.Contain("Predictions = 412");
    }

    /// <summary>
    /// An impact with exactly one category set to a single row, chosen by property name so the theory cases
    /// above stay readable and cannot drift from the property they claim to be about.
    /// </summary>
    private static UserDeletionImpactDto WithOne(string category) => category switch
    {
        nameof(UserDeletionImpactDto.SeasonPasses) => Impact(seasonPasses: 1),
        nameof(UserDeletionImpactDto.LeagueMemberships) => Impact(leagueMemberships: 1),
        nameof(UserDeletionImpactDto.Predictions) => Impact(predictions: 1),
        nameof(UserDeletionImpactDto.Winnings) => Impact(winnings: 1),
        nameof(UserDeletionImpactDto.Payouts) => Impact(payouts: 1),
        nameof(UserDeletionImpactDto.Badges) => Impact(badges: 1),
        nameof(UserDeletionImpactDto.BoostUsages) => Impact(boostUsages: 1),
        nameof(UserDeletionImpactDto.RoundResults) => Impact(roundResults: 1),
        nameof(UserDeletionImpactDto.LeagueRoundResults) => Impact(leagueRoundResults: 1),
        nameof(UserDeletionImpactDto.LeagueStandings) => Impact(leagueStandings: 1),
        nameof(UserDeletionImpactDto.EmailRecords) => Impact(emailRecords: 1),
        nameof(UserDeletionImpactDto.OnboardingSkips) => Impact(onboardingSkips: 1),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unhandled category.")
    };
}

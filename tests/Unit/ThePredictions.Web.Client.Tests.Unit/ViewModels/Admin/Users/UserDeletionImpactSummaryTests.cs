using FluentAssertions;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Web.Client.ViewModels.Admin.Users;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.ViewModels.Admin.Users;

/// <summary>
/// The wording of an irreversible warning. An administrator reads these lines and decides whether to destroy
/// somebody's history from them, so "1 season passes" or a silently omitted category both matter.
/// </summary>
public class UserDeletionImpactSummaryTests
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
    public void Describe_ShouldReturnNothing_ForAnAccountWithNoHistory()
    {
        // The dialog has its own empty case, so an empty list here is the correct answer rather than a gap.
        UserDeletionImpactSummary.Describe(Impact()).Should().BeEmpty();
    }

    [Fact]
    public void Describe_ShouldOmitEmptyCategories_SoTheLinesThatMatterAreNotBuried()
    {
        var lines = UserDeletionImpactSummary.Describe(Impact(predictions: 412));

        lines.Should().ContainSingle().Which.Should().Be("412 predictions");
    }

    [Fact]
    public void Describe_ShouldUseTheSingular_WhenACategoryHasExactlyOneRow()
    {
        UserDeletionImpactSummary.Describe(Impact(predictions: 1))
            .Should().ContainSingle().Which.Should().Be("1 prediction");
    }

    [Fact]
    public void Describe_ShouldGroupThousands_SoALongHistoryStaysReadable()
    {
        UserDeletionImpactSummary.Describe(Impact(predictions: 12345))
            .Should().ContainSingle().Which.Should().Be("12,345 predictions");
    }

    [Fact]
    public void Describe_ShouldReportWhatWasPaid_ForAPurchasedSeasonPass()
    {
        UserDeletionImpactSummary.Describe(Impact(seasonPasses: 1, seasonPassSpend: 25m))
            .Should().ContainSingle().Which.Should().Be("1 season pass (£25 paid)");
    }

    [Fact]
    public void Describe_ShouldOmitTheAmount_ForACompedOrTrialSeasonPass()
    {
        // A pass with nothing paid is a trial or an administrator's gift. "(£0.00 paid)" would read as a
        // billing fault rather than as the free pass it is.
        UserDeletionImpactSummary.Describe(Impact(seasonPasses: 1))
            .Should().ContainSingle().Which.Should().Be("1 season pass");
    }

    [Fact]
    public void Describe_ShouldShowPenceOnlyWhenThereAreAny()
    {
        UserDeletionImpactSummary.Describe(Impact(seasonPasses: 1, seasonPassSpend: 12.50m))
            .Should().ContainSingle().Which.Should().Be("1 season pass (£12.50 paid)");
    }

    [Fact]
    public void Describe_ShouldTotalPrizeWins()
    {
        UserDeletionImpactSummary.Describe(Impact(winnings: 3, winningsTotal: 47.25m))
            .Should().ContainSingle().Which.Should().Be("3 prize wins totalling £47.25");
    }

    [Fact]
    public void Describe_ShouldOmitThePrizeTotal_WhenEveryWinWasWorthNothing()
    {
        UserDeletionImpactSummary.Describe(Impact(winnings: 2))
            .Should().ContainSingle().Which.Should().Be("2 prize wins");
    }

    [Fact]
    public void Describe_ShouldTotalRecordedPayouts()
    {
        UserDeletionImpactSummary.Describe(Impact(payouts: 1, payoutsTotal: 30m))
            .Should().ContainSingle().Which.Should().Be("1 recorded payout totalling £30");
    }

    [Fact]
    public void Describe_ShouldOmitThePayoutTotal_WhenNothingWasOwed()
    {
        UserDeletionImpactSummary.Describe(Impact(payouts: 1))
            .Should().ContainSingle().Which.Should().Be("1 recorded payout");
    }

    [Fact]
    public void Describe_ShouldMentionBankDetails_WhenTheyAreStored()
    {
        // Not a count - there is at most one row - so it is phrased rather than numbered.
        UserDeletionImpactSummary.Describe(Impact(hasPayoutDetails: true))
            .Should().ContainSingle().Which.Should().Be("Their saved bank details");
    }

    [Fact]
    public void Describe_ShouldSayNothingAboutBankDetails_WhenNoneAreStored()
    {
        UserDeletionImpactSummary.Describe(Impact(predictions: 1))
            .Should().NotContain(line => line.Contains("bank", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Describe_ShouldPutMoneyFirst_BecauseItIsWhatCannotBeReconstructed()
    {
        var lines = UserDeletionImpactSummary.Describe(Impact(
            seasonPasses: 1,
            seasonPassSpend: 25m,
            predictions: 40,
            winnings: 1,
            winningsTotal: 10m,
            onboardingSkips: 2));

        lines.Should().HaveCount(4);
        lines[0].Should().Contain("season pass");
        lines[1].Should().Contain("prize win");
        lines[2].Should().Contain("prediction");
        lines[3].Should().Contain("onboarding");
    }

    [Fact]
    public void Describe_ShouldCoverEveryCategoryTheDeleteDestroys()
    {
        // The whole point of the dialog is that it is complete. A category added to the DTO but forgotten here
        // would silently go undeclared to the administrator, which is the failure this test exists to catch.
        var lines = UserDeletionImpactSummary.Describe(Impact(
            seasonPasses: 1,
            leagueMemberships: 2,
            predictions: 3,
            winnings: 4,
            payouts: 5,
            badges: 6,
            boostUsages: 7,
            roundResults: 8,
            leagueRoundResults: 9,
            leagueStandings: 10,
            emailRecords: 11,
            onboardingSkips: 12,
            hasPayoutDetails: true));

        lines.Should().BeEquivalentTo(new[]
        {
            "1 season pass",
            "4 prize wins",
            "5 recorded payouts",
            "Their saved bank details",
            "2 league memberships",
            "3 predictions",
            "7 boosts played",
            "6 badges earned",
            "9 league round scores",
            "8 overall round results",
            "10 league standings",
            "11 email records",
            "12 dismissed onboarding tips"
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Describe_ShouldIgnoreAdministeredLeagues()
    {
        // They survive the deletion and are re-assigned, so listing them under "this will permanently delete"
        // would be a lie. The dialog covers them in its own separate sentence.
        UserDeletionImpactSummary.Describe(Impact(leaguesAdministered: 4)).Should().BeEmpty();
    }
}

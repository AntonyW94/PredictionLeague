using FluentAssertions;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Contracts.Onboarding;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Contracts.Tests.Unit.Admin.Users;

/// <summary>
/// The account as the administrator's list shows it.
///
/// The figures themselves are the handler's to work out. What is tested here is the questions the screen asks of them -
/// which of three Season Pass states an account is in, how far it got through setup, and the one combination that needs
/// acting on: money won with nowhere to send it.
/// </summary>
public class UserDtoTests
{
    private static UserDto User(
        bool hasEverHeldSeasonPass = false,
        bool hasCurrentSeasonPass = false,
        bool hasEverPurchasedSeasonPass = false,
        int leaguesCreated = 0,
        int leaguesJoinedApproved = 0,
        int leaguesJoinedPending = 0,
        decimal totalWinnings = 0m,
        decimal seasonPassSpend = 0m,
        decimal leagueEntrySpend = 0m,
        bool termsAccepted = true,
        bool marketingOptIn = false,
        bool hasPayoutDetails = false,
        OnboardingChecklistDto? onboarding = null,
        List<string>? socialProviders = null,
        IReadOnlyList<UserSeasonPassDto>? seasonPasses = null,
        IReadOnlyList<UserPrizeDto>? prizes = null,
        IReadOnlyList<UserBadgeDto>? badges = null) =>
        new("user-1", "Alex Player", "alex@example.com", null, false, true, socialProviders ?? [], true,
            termsAccepted, marketingOptIn, hasPayoutDetails,
            hasEverHeldSeasonPass, hasCurrentSeasonPass, hasEverPurchasedSeasonPass,
            leaguesCreated, leaguesJoinedApproved, leaguesJoinedPending,
            totalWinnings, seasonPassSpend, leagueEntrySpend,
            onboarding ?? Checklist(),
            [], [], seasonPasses ?? [], prizes ?? [], badges ?? []);

    private static OnboardingChecklistDto Checklist(params string[] states) =>
        new(RequiredComplete: false, HasOutstandingSteps: false,
            states.Select((state, index) => new OnboardingStepDto($"step-{index}", $"Step {index}", true, false, state, "Go", "/")).ToList());

    private static UserSeasonPassDto Pass(int seasonId, string seasonName, bool isCurrentSeason) =>
        new(seasonId, seasonName, isCurrentSeason, SeasonPassTier.Standard, SeasonPassSource.Purchased, 10m, 0m, DateTime.UnixEpoch);

    #region Money

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

    #endregion

    #region Season passes

    [Fact]
    public void CurrentPassSeasonNames_ShouldNameOnlyTheSeasonsStillRunning()
    {
        // The whole reason "has a Season Pass" was not good enough: a pass for a finished season is not a pass to play.
        var user = User(seasonPasses:
        [
            Pass(3, "Premier League 2026/27", isCurrentSeason: true),
            Pass(2, "World Cup 2026", isCurrentSeason: false)
        ]);

        user.CurrentPassSeasonNames.Should().Equal("Premier League 2026/27");
    }

    [Fact]
    public void CurrentPassSeasonNames_ShouldListTheNewestSeasonFirst_WhenTwoAreRunningAtOnce()
    {
        // A tournament overlapping a league season, which happens most summers.
        var user = User(seasonPasses:
        [
            Pass(2, "World Cup 2026", isCurrentSeason: true),
            Pass(3, "Premier League 2026/27", isCurrentSeason: true)
        ]);

        user.CurrentPassSeasonNames.Should().Equal("Premier League 2026/27", "World Cup 2026");
    }

    [Fact]
    public void CurrentPassSeasonNames_ShouldBeEmpty_ForAnAccountWhosePassesHaveAllExpired()
    {
        User(seasonPasses: [Pass(1, "Premier League 2025/26", isCurrentSeason: false)])
            .CurrentPassSeasonNames.Should().BeEmpty();
    }

    #endregion

    #region Counts

    [Fact]
    public void BadgeCount_ShouldCountTheBadgesEarned()
    {
        var user = User(badges:
        [
            new UserBadgeDto("champion", "Champion", null, DateTime.UnixEpoch, null, null),
            new UserBadgeDto("veteran", "Veteran", null, DateTime.UnixEpoch, null, null)
        ]);

        user.BadgeCount.Should().Be(2);
    }

    [Fact]
    public void PrizeCount_ShouldCountThePrizesWonRatherThanTheirValue()
    {
        var user = User(prizes:
        [
            new UserPrizeDto(1, "The League", 3, "Season", true, "Overall winner", 90m, DateTime.UnixEpoch),
            new UserPrizeDto(1, "The League", 3, "Season", true, "Round 4 winner", 5m, DateTime.UnixEpoch)
        ]);

        user.PrizeCount.Should().Be(2);
    }

    [Fact]
    public void BadgeCountAndPrizeCount_ShouldBeZero_ForAnAccountWithNeither()
    {
        var user = User();

        user.BadgeCount.Should().Be(0);
        user.PrizeCount.Should().Be(0);
    }

    #endregion

    #region Onboarding

    [Fact]
    public void OnboardingStepsCompleted_ShouldCountOnlyTheCompletedSteps()
    {
        // Skipped is not done. That is the difference between "would not" and "has not", and the popup shows which.
        var user = User(onboarding: Checklist(
            OnboardingStepStates.Completed,
            OnboardingStepStates.Completed,
            OnboardingStepStates.Active,
            OnboardingStepStates.Skipped));

        user.OnboardingStepsCompleted.Should().Be(2);
        user.OnboardingStepCount.Should().Be(4);
        user.OnboardingComplete.Should().BeFalse();
    }

    [Fact]
    public void OnboardingComplete_ShouldBeTrue_WhenEveryStepIsDone()
    {
        var user = User(onboarding: Checklist(OnboardingStepStates.Completed, OnboardingStepStates.Completed));

        user.OnboardingComplete.Should().BeTrue();
    }

    [Fact]
    public void OnboardingComplete_ShouldBeFalse_WhenThereAreNoStepsAtAll()
    {
        // Not a state the registry produces, but "nought out of nought" must not read as finished - the filter for
        // incomplete setup would then hide the very accounts it exists to find.
        var user = User(onboarding: Checklist());

        user.OnboardingStepCount.Should().Be(0);
        user.OnboardingComplete.Should().BeFalse();
    }

    [Fact]
    public void OnboardingComplete_ShouldBeFalse_WhenAStepIsLocked()
    {
        var user = User(onboarding: Checklist(OnboardingStepStates.Completed, OnboardingStepStates.Locked));

        user.OnboardingComplete.Should().BeFalse();
    }

    #endregion

    #region Dormant

    [Fact]
    public void IsDormant_ShouldBeTrue_ForAUserWithNoPassAndNoLeagues()
    {
        User().IsDormant.Should().BeTrue();
    }

    [Fact]
    public void IsDormant_ShouldBeFalse_WhenTheUserHasEverHeldASeasonPass()
    {
        // Deliberately "ever", not "currently". Somebody who played last season and has not come back is lapsed, not
        // dormant, and the two want different chasing - which is why there is a separate no-current-pass filter.
        User(hasEverHeldSeasonPass: true).IsDormant.Should().BeFalse();
    }

    [Fact]
    public void IsDormant_ShouldBeTrue_ForAnAccountWithACurrentPassFlagButNoPassHistory()
    {
        // Defends the choice above: dormancy keys on the "ever" flag only, so the other two cannot quietly change it.
        User(hasCurrentSeasonPass: true, hasEverPurchasedSeasonPass: true).IsDormant.Should().BeTrue();
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

    #endregion

    #region Owed money with nowhere to send it

    [Fact]
    public void IsOwedMoneyWithNowhereToSendIt_ShouldBeTrue_ForAWinnerWithNoPayoutDetails()
    {
        User(totalWinnings: 290m, hasPayoutDetails: false).IsOwedMoneyWithNowhereToSendIt.Should().BeTrue();
    }

    [Fact]
    public void IsOwedMoneyWithNowhereToSendIt_ShouldBeFalse_WhenTheWinnerHasPayoutDetails()
    {
        User(totalWinnings: 290m, hasPayoutDetails: true).IsOwedMoneyWithNowhereToSendIt.Should().BeFalse();
    }

    [Fact]
    public void IsOwedMoneyWithNowhereToSendIt_ShouldBeFalse_ForAnAccountThatHasWonNothing()
    {
        // Most accounts have no payout details and that is fine. It only matters once there is money to send.
        User(totalWinnings: 0m, hasPayoutDetails: false).IsOwedMoneyWithNowhereToSendIt.Should().BeFalse();
    }

    #endregion

    #region Record semantics

    [Fact]
    public void TwoUsersSharingEveryValueShouldBeEqual()
    {
        // Note the shared SocialProviders instance. A record compares collection members by
        // reference, so two separately-built lists would make otherwise identical users unequal.
        var socialProviders = new List<string> { "Google" };
        var onboarding = Checklist(OnboardingStepStates.Completed);

        var first = User(socialProviders: socialProviders, onboarding: onboarding);
        var second = User(socialProviders: socialProviders, onboarding: onboarding);

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

    #endregion
}

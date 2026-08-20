using FluentAssertions;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Contracts.Onboarding;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Web.Client.ViewModels.Admin.Users;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.ViewModels.Admin.Users;

/// <summary>
/// Which accounts the administrator sees, and in what order.
///
/// The filters matter more than they look. Most of these figures are zero for most accounts, so the tie-break on name is
/// what stops the list reshuffling between loads - and the five toggles have to combine, because "lapsed, and we owe them
/// money" is the pair worth acting on first.
/// </summary>
public class UserListCriteriaTests
{
    #region Tabs

    [Fact]
    public void Apply_ShouldReturnEverybody_OnTheAllTab()
    {
        var users = new[] { User("Ada", isAdmin: true), User("Grace") };

        Criteria().Apply(users).Should().HaveCount(2);
    }

    [Fact]
    public void Apply_ShouldReturnOnlyAdministrators_OnTheAdminTab()
    {
        var users = new[] { User("Ada", isAdmin: true), User("Grace") };

        Criteria(tab: UserListTab.Admin).Apply(users).Select(user => user.FullName).Should().Equal("Ada");
    }

    [Fact]
    public void Apply_ShouldReturnOnlyPlayers_OnThePlayerTab()
    {
        var users = new[] { User("Ada", isAdmin: true), User("Grace") };

        Criteria(tab: UserListTab.Player).Apply(users).Select(user => user.FullName).Should().Equal("Grace");
    }

    [Fact]
    public void InTab_ShouldNotApplyTheFilters()
    {
        // What the "showing n of m" line counts against, so it has to be the tab before any filter narrows it.
        var users = new[] { User("Ada", isDormant: true), User("Grace") };

        Criteria(dormantOnly: true).InTab(users).Should().HaveCount(2);
    }

    #endregion

    #region Search

    [Fact]
    public void Apply_ShouldMatchOnPartOfTheName()
    {
        var users = new[] { User("Ada Lovelace"), User("Grace Hopper") };

        Criteria(searchTerm: "love").Apply(users).Select(user => user.FullName).Should().Equal("Ada Lovelace");
    }

    [Fact]
    public void Apply_ShouldMatchOnPartOfTheEmail()
    {
        var users = new[] { User("Ada", email: "ada@example.com"), User("Grace", email: "grace@other.com") };

        Criteria(searchTerm: "OTHER").Apply(users).Select(user => user.FullName).Should().Equal("Grace");
    }

    [Fact]
    public void Apply_ShouldMatchOnPartOfTheMobileNumber()
    {
        // The reason the number is on the card at all: a number rings in, and the question is whose it is.
        var users = new[] { User("Ada", phoneNumber: "07700900123"), User("Grace", phoneNumber: "07700900456") };

        Criteria(searchTerm: "900456").Apply(users).Select(user => user.FullName).Should().Equal("Grace");
    }

    [Fact]
    public void Apply_ShouldNotMatchAccountsWithNoMobileNumber_WhenSearchingForOne()
    {
        // Most accounts have no number, so the search runs against null far more often than not.
        var users = new[] { User("Ada", phoneNumber: null), User("Grace", phoneNumber: "07700900456") };

        Criteria(searchTerm: "07700").Apply(users).Select(user => user.FullName).Should().Equal("Grace");
    }

    [Fact]
    public void Apply_ShouldIgnoreSurroundingSpaceInTheSearchTerm()
    {
        // Typed, or pasted from somewhere else. Neither should return nothing.
        var users = new[] { User("Ada Lovelace") };

        Criteria(searchTerm: "  Lovelace  ").Apply(users).Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Apply_ShouldNotFilter_WhenTheSearchTermIsEffectivelyEmpty(string? term)
    {
        var users = new[] { User("Ada"), User("Grace") };

        Criteria(searchTerm: term).Apply(users).Should().HaveCount(2);
    }

    #endregion

    #region Filters

    [Fact]
    public void Apply_ShouldKeepOnlyDormantAccounts_WhenDormantOnlyIsOn()
    {
        var users = new[] { User("Ada", isDormant: true), User("Grace") };

        Criteria(dormantOnly: true).Apply(users).Select(user => user.FullName).Should().Equal("Ada");
    }

    [Fact]
    public void Apply_ShouldKeepOnlyAccountsWithNoCurrentPass_WhenThatFilterIsOn()
    {
        // The chase list: everybody who cannot play the season that is running.
        var users = new[] { User("Ada", hasCurrentPass: true), User("Grace") };

        Criteria(noCurrentPassOnly: true).Apply(users).Select(user => user.FullName).Should().Equal("Grace");
    }

    [Fact]
    public void Apply_ShouldKeepOnlyAccountsPartWayThroughSetup_WhenThatFilterIsOn()
    {
        var users = new[] { User("Ada", stepsCompleted: 4), User("Grace", stepsCompleted: 2) };

        Criteria(setupIncompleteOnly: true).Apply(users).Select(user => user.FullName).Should().Equal("Grace");
    }

    [Fact]
    public void Apply_ShouldKeepOnlyWinnersWithNoPayoutDetails_WhenThatFilterIsOn()
    {
        var users = new[]
        {
            User("Ada", winnings: 290m, hasPayoutDetails: false),
            User("Grace", winnings: 290m, hasPayoutDetails: true),
            User("Wanda", winnings: 0m, hasPayoutDetails: false)
        };

        Criteria(unpaidWinnersOnly: true).Apply(users).Select(user => user.FullName).Should().Equal("Ada");
    }

    [Fact]
    public void Apply_ShouldNotTreatSkippedStepsAsIncompleteSetup()
    {
        // Dismissing an optional step is an answer. "Ada" did the two required steps and said no to the two optional
        // ones, so there is nothing left to chase her about; "Grace" has never touched hers, so there is.
        var users = new[]
        {
            User("Ada", stepsCompleted: 2, stepsSkipped: 2),
            User("Grace", stepsCompleted: 2)
        };

        Criteria(setupIncompleteOnly: true).Apply(users).Select(user => user.FullName).Should().Equal("Grace");
    }

    [Fact]
    public void Apply_ShouldKeepOnlyAccountsWithAnUnconfirmedEmail_WhenThatFilterIsOn()
    {
        // An account that never confirmed has had no email from us that mattered, which is worth being able to isolate.
        var users = new[] { User("Ada", emailConfirmed: false), User("Grace") };

        Criteria(emailUnconfirmedOnly: true).Apply(users).Select(user => user.FullName).Should().Equal("Ada");
    }

    [Fact]
    public void Apply_ShouldCombineFilters()
    {
        // The combination the screen exists for: lapsed, and owed money we cannot send.
        var users = new[]
        {
            User("Ada", hasCurrentPass: false, winnings: 290m, hasPayoutDetails: false),
            User("Grace", hasCurrentPass: true, winnings: 290m, hasPayoutDetails: false),
            User("Wanda", hasCurrentPass: false, winnings: 0m, hasPayoutDetails: false)
        };

        Criteria(noCurrentPassOnly: true, unpaidWinnersOnly: true).Apply(users)
            .Select(user => user.FullName).Should().Equal("Ada");
    }

    [Fact]
    public void Apply_ShouldReturnNothing_WhenNoAccountMatches()
    {
        var users = new[] { User("Ada") };

        Criteria(searchTerm: "nobody").Apply(users).Should().BeEmpty();
    }

    #endregion

    #region Sorting

    [Fact]
    public void Apply_ShouldSortByNameByDefault()
    {
        var users = new[] { User("Wanda"), User("Ada") };

        Criteria().Apply(users).Select(user => user.FullName).Should().Equal("Ada", "Wanda");
    }

    [Fact]
    public void Apply_ShouldSortByNameIgnoringCase()
    {
        var users = new[] { User("bob"), User("Ada"), User("Zara") };

        Criteria().Apply(users).Select(user => user.FullName).Should().Equal("Ada", "bob", "Zara");
    }

    [Fact]
    public void Apply_ShouldReverseTheNameOrder_WhenSortingDescending()
    {
        var users = new[] { User("Ada"), User("Wanda") };

        Criteria(sortDescending: true).Apply(users).Select(user => user.FullName).Should().Equal("Wanda", "Ada");
    }

    [Theory]
    [InlineData(UserListSortField.LeaguesCreated)]
    [InlineData(UserListSortField.LeaguesJoined)]
    [InlineData(UserListSortField.Badges)]
    [InlineData(UserListSortField.PassSpend)]
    [InlineData(UserListSortField.EntrySpend)]
    [InlineData(UserListSortField.TotalSpend)]
    [InlineData(UserListSortField.Winnings)]
    [InlineData(UserListSortField.Setup)]
    public void Apply_ShouldFallBackToNameWithinAnEqualGroup_ForEverySortField(UserListSortField field)
    {
        // What stops the list reshuffling. Every one of these fields is zero for most accounts, so without the tie-break
        // dozens of rows keep whatever order they arrived in and move about between loads.
        var users = new[] { User("Wanda"), User("Ada"), User("Grace") };

        Criteria(sortField: field).Apply(users)
            .Select(user => user.FullName).Should().Equal("Ada", "Grace", "Wanda");
    }

    [Fact]
    public void Apply_ShouldKeepTheNameTieBreakAscending_WhenSortingDescending()
    {
        // Descending reverses the chosen field, not the tie-break, so names stay A-Z inside an equal group.
        var users = new[] { User("Wanda", winnings: 10m), User("Grace"), User("Ada") };

        Criteria(sortField: UserListSortField.Winnings, sortDescending: true).Apply(users)
            .Select(user => user.FullName).Should().Equal("Wanda", "Ada", "Grace");
    }

    [Fact]
    public void Apply_ShouldSortByLeaguesCreated()
    {
        var users = new[] { User("Ada", leaguesCreated: 6), User("Grace", leaguesCreated: 1) };

        Criteria(sortField: UserListSortField.LeaguesCreated).Apply(users)
            .Select(user => user.FullName).Should().Equal("Grace", "Ada");
    }

    [Fact]
    public void Apply_ShouldSortByLeaguesJoinedIncludingRequestsStillPending()
    {
        // A pending request is an account taking part, so it counts towards how busy they are.
        var users = new[]
        {
            User("Ada", leaguesJoinedApproved: 2, leaguesJoinedPending: 0),
            User("Grace", leaguesJoinedApproved: 1, leaguesJoinedPending: 3)
        };

        Criteria(sortField: UserListSortField.LeaguesJoined).Apply(users)
            .Select(user => user.FullName).Should().Equal("Ada", "Grace");
    }

    [Fact]
    public void Apply_ShouldSortByBadges()
    {
        var users = new[] { User("Ada", badgeCount: 49), User("Grace", badgeCount: 2) };

        Criteria(sortField: UserListSortField.Badges, sortDescending: true).Apply(users)
            .Select(user => user.FullName).Should().Equal("Ada", "Grace");
    }

    [Fact]
    public void Apply_ShouldSortPassSpendAndEntrySpendSeparately()
    {
        // The whole point of splitting them: an account can be top of one and bottom of the other.
        var users = new[]
        {
            User("Ada", passSpend: 10m, entrySpend: 25m),
            User("Grace", passSpend: 0m, entrySpend: 100m)
        };

        Criteria(sortField: UserListSortField.PassSpend, sortDescending: true).Apply(users)
            .Select(user => user.FullName).Should().Equal("Ada", "Grace");

        Criteria(sortField: UserListSortField.EntrySpend, sortDescending: true).Apply(users)
            .Select(user => user.FullName).Should().Equal("Grace", "Ada");
    }

    [Fact]
    public void Apply_ShouldSortByTotalSpend()
    {
        var users = new[]
        {
            User("Ada", passSpend: 10m, entrySpend: 25m),
            User("Grace", passSpend: 0m, entrySpend: 100m)
        };

        Criteria(sortField: UserListSortField.TotalSpend, sortDescending: true).Apply(users)
            .Select(user => user.FullName).Should().Equal("Grace", "Ada");
    }

    [Fact]
    public void Apply_ShouldSortByWinnings()
    {
        var users = new[] { User("Ada", winnings: 146.50m), User("Grace", winnings: 290m) };

        Criteria(sortField: UserListSortField.Winnings, sortDescending: true).Apply(users)
            .Select(user => user.FullName).Should().Equal("Grace", "Ada");
    }

    [Fact]
    public void Apply_ShouldSortBySetupProgress()
    {
        var users = new[] { User("Ada", stepsCompleted: 4), User("Grace", stepsCompleted: 1) };

        Criteria(sortField: UserListSortField.Setup).Apply(users)
            .Select(user => user.FullName).Should().Equal("Grace", "Ada");
    }

    [Fact]
    public void Apply_ShouldReturnNothing_WhenThereAreNoAccounts()
    {
        Criteria().Apply([]).Should().BeEmpty();
    }

    #endregion

    #region The state of the controls

    [Fact]
    public void Default_ShouldHaveNothingActive()
    {
        UserListCriteria.Default.HasActiveFilters.Should().BeFalse();
        UserListCriteria.Default.HasNonDefaultControls.Should().BeFalse();
        UserListCriteria.Default.Tab.Should().Be(UserListTab.All);
        UserListCriteria.Default.SortField.Should().Be(UserListSortField.Name);
    }

    [Theory]
    [InlineData("ada", false, false, false, false, false)]
    [InlineData(null, true, false, false, false, false)]
    [InlineData(null, false, true, false, false, false)]
    [InlineData(null, false, false, true, false, false)]
    [InlineData(null, false, false, false, true, false)]
    [InlineData(null, false, false, false, false, true)]
    public void HasActiveFilters_ShouldBeTrue_WhenAnythingIsBeingHidden(
        string? searchTerm, bool dormant, bool noPass, bool setupIncomplete, bool unpaidWinners, bool emailUnconfirmed)
    {
        Criteria(searchTerm: searchTerm, dormantOnly: dormant, noCurrentPassOnly: noPass,
                 setupIncompleteOnly: setupIncomplete, unpaidWinnersOnly: unpaidWinners,
                 emailUnconfirmedOnly: emailUnconfirmed)
            .HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public void HasActiveFilters_ShouldBeFalse_WhenOnlyTheSortHasChanged()
    {
        // A re-sort hides nothing, so the "showing n of m" line has no business appearing.
        Criteria(sortField: UserListSortField.Winnings, sortDescending: true).HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public void HasNonDefaultControls_ShouldBeTrue_WhenOnlyTheSortHasChanged()
    {
        // Drives the marker on the collapsed toolbar, which does have to appear for a re-sort - otherwise somebody who
        // sorted by winnings and collapsed the panel has no way of knowing why the order looks wrong.
        Criteria(sortField: UserListSortField.Winnings).HasNonDefaultControls.Should().BeTrue();
        Criteria(sortDescending: true).HasNonDefaultControls.Should().BeTrue();
    }

    [Fact]
    public void HasNonDefaultControls_ShouldBeTrue_WhenAFilterIsOn()
    {
        Criteria(dormantOnly: true).HasNonDefaultControls.Should().BeTrue();
    }

    [Fact]
    public void HasNonDefaultControls_ShouldBeFalse_WhenOnlyTheTabHasChanged()
    {
        // The tab is a place in the screen, not a control that has been moved off its default.
        Criteria(tab: UserListTab.Admin).HasNonDefaultControls.Should().BeFalse();
    }

    [Fact]
    public void Cleared_ShouldResetEveryFilterAndSortButKeepTheTab()
    {
        var criteria = Criteria(
            tab: UserListTab.Player,
            searchTerm: "ada",
            sortField: UserListSortField.Winnings,
            sortDescending: true,
            dormantOnly: true,
            noCurrentPassOnly: true,
            setupIncompleteOnly: true,
            unpaidWinnersOnly: true,
            emailUnconfirmedOnly: true);

        var cleared = criteria.Cleared();

        cleared.Tab.Should().Be(UserListTab.Player);
        cleared.HasNonDefaultControls.Should().BeFalse();
        cleared.SearchTerm.Should().BeNull();
        cleared.SortField.Should().Be(UserListSortField.Name);
        cleared.SortDescending.Should().BeFalse();
    }

    #endregion

    private static UserListCriteria Criteria(
        UserListTab tab = UserListTab.All,
        string? searchTerm = null,
        UserListSortField sortField = UserListSortField.Name,
        bool sortDescending = false,
        bool dormantOnly = false,
        bool noCurrentPassOnly = false,
        bool setupIncompleteOnly = false,
        bool unpaidWinnersOnly = false,
        bool emailUnconfirmedOnly = false) =>
        new(tab, searchTerm, sortField, sortDescending, dormantOnly, noCurrentPassOnly, setupIncompleteOnly,
            unpaidWinnersOnly, emailUnconfirmedOnly);

    /// <summary>
    /// An account with only the facts a test states.
    /// </summary>
    /// <remarks>
    /// <paramref name="isDormant"/> and <paramref name="hasCurrentPass"/> are separate knobs because the DTO derives
    /// dormancy from having <b>ever</b> held a pass and the filter reads the current one - a test has to be able to arrange
    /// an account that is one and not the other.
    /// </remarks>
    private static UserDto User(
        string name,
        bool isAdmin = false,
        string? email = null,
        string? phoneNumber = null,
        bool emailConfirmed = true,
        bool isDormant = false,
        bool hasCurrentPass = false,
        int stepsCompleted = 4,
        int stepsSkipped = 0,
        int leaguesCreated = 0,
        int leaguesJoinedApproved = 0,
        int leaguesJoinedPending = 0,
        int badgeCount = 0,
        decimal passSpend = 0m,
        decimal entrySpend = 0m,
        decimal winnings = 0m,
        bool hasPayoutDetails = true,
        DateTime? createdAtUtc = null) =>
        new($"id-{name}", name, email ?? $"{name}@example.com", phoneNumber, createdAtUtc, isAdmin, true, [], emailConfirmed,
            TermsAccepted: true, MarketingOptIn: false, hasPayoutDetails,
            HasEverHeldSeasonPass: !isDormant, hasCurrentPass, HasEverPurchasedSeasonPass: false,
            leaguesCreated, leaguesJoinedApproved, leaguesJoinedPending,
            winnings, passSpend, entrySpend,
            Checklist(stepsCompleted, stepsSkipped),
            Memberships: [], AdministeredLeagues: [], SeasonPasses: [], Prizes: [],
            Badges: Badges(badgeCount));

    /// <summary>
    /// Four steps: the first <paramref name="completed"/> done, the next <paramref name="skipped"/> dismissed, and
    /// whatever is left still outstanding.
    /// </summary>
    /// <remarks>
    /// <c>HasOutstandingSteps</c> is derived rather than passed in, by the rule <c>OnboardingStepRegistry.Build</c>
    /// applies: only an Active or Locked step is outstanding. That is what the setup filter now reads, so a fixture
    /// that hard-coded the flag could assert a checklist the server never produces.
    /// </remarks>
    private static OnboardingChecklistDto Checklist(int completed, int skipped = 0) =>
        new(RequiredComplete: completed >= 2, HasOutstandingSteps: completed + skipped < 4,
            Enumerable.Range(0, 4)
                .Select(index => new OnboardingStepDto(
                    $"step-{index}", $"Step {index}", index < 2, index >= 2,
                    index < completed ? OnboardingStepStates.Completed
                        : index < completed + skipped ? OnboardingStepStates.Skipped
                        : OnboardingStepStates.Active,
                    "Go", "/"))
                .ToList());

    private static UserBadgeDto[] Badges(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new UserBadgeDto($"badge-{index}", $"Badge {index}", null, DateTime.UnixEpoch, null, null))
            .ToArray();
}

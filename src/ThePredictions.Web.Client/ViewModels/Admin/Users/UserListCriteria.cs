using ThePredictions.Contracts.Admin.Users;

namespace ThePredictions.Web.Client.ViewModels.Admin.Users;

/// <summary>
/// Which accounts the administrator wants to see, and in what order.
/// </summary>
/// <remarks>
/// A value rather than a set of fields on the page, so that what each control does is stated somewhere a test can reach.
/// The page owns one of these and replaces it; the sorting and filtering itself happens here.
///
/// The filters are deliberately independent rather than one dropdown, because they answer different questions and an
/// administrator often wants two at once - "lapsed, and we owe them money" is the combination worth acting on first.
/// </remarks>
public sealed record UserListCriteria(
    UserListTab Tab = UserListTab.All,
    string? SearchTerm = null,
    UserListSortField SortField = UserListSortField.Name,
    bool SortDescending = false,
    bool DormantOnly = false,
    bool NoCurrentPassOnly = false,
    bool SetupIncompleteOnly = false,
    bool UnpaidWinnersOnly = false)
{
    /// <summary>What the screen shows before anybody touches a control.</summary>
    public static UserListCriteria Default { get; } = new();

    /// <summary>Whether anything is being hidden, which is what the "showing n of m" line is for.</summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm)
        || DormantOnly
        || NoCurrentPassOnly
        || SetupIncompleteOnly
        || UnpaidWinnersOnly;

    /// <summary>
    /// Whether any control has been moved off its default, filter or sort.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="HasActiveFilters"/> because they drive different things: the marker on the collapsed
    /// toolbar has to appear for a re-sort as well, or somebody who sorted by winnings and collapsed the panel has no way
    /// of knowing why the order looks wrong.
    /// </remarks>
    public bool HasNonDefaultControls =>
        HasActiveFilters || SortField != UserListSortField.Name || SortDescending;

    /// <summary>Back to the defaults, keeping the tab - the tab is a place in the screen, not a filter.</summary>
    public UserListCriteria Cleared() => Default with { Tab = Tab };

    /// <summary>
    /// The accounts to show, in order.
    /// </summary>
    /// <remarks>
    /// The tab is applied first because the counts beside the tab names are of everything in that tab, and the "showing n
    /// of m" line compares against the same set.
    /// </remarks>
    public IReadOnlyList<UserDto> Apply(IEnumerable<UserDto> users) =>
        Sort(Filter(InTab(users))).ToList();

    /// <summary>Everything in the chosen tab, before any filter.</summary>
    public IEnumerable<UserDto> InTab(IEnumerable<UserDto> users) => Tab switch
    {
        UserListTab.Admin => users.Where(user => user.IsAdmin),
        UserListTab.Player => users.Where(user => !user.IsAdmin),
        _ => users
    };

    private IEnumerable<UserDto> Filter(IEnumerable<UserDto> users)
    {
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim();
            users = users.Where(user =>
                user.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || user.Email.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (DormantOnly)
            users = users.Where(user => user.IsDormant);

        if (NoCurrentPassOnly)
            users = users.Where(user => !user.HasCurrentSeasonPass);

        if (SetupIncompleteOnly)
            users = users.Where(user => !user.OnboardingComplete);

        if (UnpaidWinnersOnly)
            users = users.Where(user => user.IsOwedMoneyWithNowhereToSendIt);

        return users;
    }

    /// <summary>
    /// Ordered by the chosen field, and by name within it.
    /// </summary>
    /// <remarks>
    /// The name tie-break is what stops the list reshuffling. Most of these fields are zero for most accounts, so sorting
    /// on one alone leaves dozens of rows in whatever order they arrived and they move about between loads.
    ///
    /// Descending reverses the chosen field only, not the tie-break, so names stay A-Z inside an equal group either way.
    /// </remarks>
    private IEnumerable<UserDto> Sort(IEnumerable<UserDto> users)
    {
        var ordered = SortField switch
        {
            UserListSortField.LeaguesCreated => By(users, user => user.LeaguesCreated),
            UserListSortField.LeaguesJoined => By(users, user => user.LeaguesJoinedApproved + user.LeaguesJoinedPending),
            UserListSortField.Badges => By(users, user => user.BadgeCount),
            UserListSortField.PassSpend => By(users, user => user.SeasonPassSpend),
            UserListSortField.EntrySpend => By(users, user => user.LeagueEntrySpend),
            UserListSortField.TotalSpend => By(users, user => user.TotalSpend),
            UserListSortField.Winnings => By(users, user => user.TotalWinnings),
            UserListSortField.Setup => By(users, user => user.OnboardingStepsCompleted),
            _ => ByName(users)
        };

        return ordered.ThenBy(user => user.FullName, StringComparer.InvariantCultureIgnoreCase);
    }

    private IOrderedEnumerable<UserDto> By<TKey>(IEnumerable<UserDto> users, Func<UserDto, TKey> key) =>
        SortDescending
            ? users.OrderByDescending(key)
            : users.OrderBy(key);

    /// <summary>
    /// Name is its own case because it is the tie-break as well as a sort, and ordering by it twice does nothing.
    /// </summary>
    private IOrderedEnumerable<UserDto> ByName(IEnumerable<UserDto> users) =>
        SortDescending
            ? users.OrderByDescending(user => user.FullName, StringComparer.InvariantCultureIgnoreCase)
            : users.OrderBy(user => user.FullName, StringComparer.InvariantCultureIgnoreCase);
}

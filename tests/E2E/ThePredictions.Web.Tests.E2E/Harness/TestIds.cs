namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Every <c>data-test-id</c> the suite looks for, in one place.
///
/// Tests address elements by these and nothing else. A CSS class is a styling hook - renaming one is a
/// perfectly ordinary thing to do to a stylesheet, and it should not be able to break a test. An id put
/// there for a test is a stated contract: it says "something depends on this", so it survives a restyle
/// and its removal is a deliberate act rather than an accident.
/// </summary>
/// <remarks>
/// Constants rather than literals at the call sites, so the markup side has exactly one thing to grep for
/// and <see cref="TestIdConventionTests"/> can prove every one of them actually exists in the markup -
/// which is the half a compiler cannot check, since the other end of this contract is a string in a
/// <c>.razor</c> file.
///
/// Naming: kebab-case, prefixed by the area of the app the element belongs to, so the set stays readable
/// as it grows and two areas can both have, say, a submit button.
/// </remarks>
internal static class TestIds
{
    /// <summary>The attribute itself. Playwright defaults to <c>data-testid</c>, so it is reconfigured to
    /// this in <see cref="StackFixture"/> - see the note there.</summary>
    internal const string Attribute = "data-test-id";

    internal const string LoginEmail = "login-email";
    internal const string LoginPassword = "login-password";
    internal const string LoginSubmit = "login-submit";

    internal const string CookieConsent = "cookie-consent";
    internal const string CookieConsentReject = "cookie-consent-reject";

    internal const string NavAccountMenu = "nav-account-menu";

    internal const string Dashboard = "dashboard";

    /// <summary>
    /// The primary action on a My Leagues card. Labelled "View Dashboard" mid-season and "View Recap" once
    /// finished, which is exactly why it is addressed by id rather than by its text.
    /// </summary>
    internal const string MyLeaguesView = "my-leagues-view";

    /// <summary>
    /// The join-a-private-league-by-code flow, which is two steps: enter the code and fetch a preview, then
    /// confirm. Each step is addressable so a failure names the step that broke rather than the journey.
    /// </summary>
    internal const string JoinPrivateOpen = "join-private-open";

    internal const string JoinPrivateModal = "join-private-modal";

    internal const string JoinEntryCode = "join-entry-code";

    internal const string JoinContinue = "join-continue";

    internal const string JoinPreview = "join-preview";

    internal const string JoinConfirm = "join-confirm";

    internal const string JoinSent = "join-sent";

    internal const string OverallLeaderboard = "overall-leaderboard";

    /// <summary>
    /// One per row, so several elements share it. That is fine, and is what makes "at least one row" and
    /// "how many rows" both expressible.
    /// </summary>
    internal const string LeaderboardRow = "leaderboard-row";

    /// <summary>
    /// The countdown a league page shows instead of its content while the competition has not started.
    /// Asserted <b>absent</b> on the leaderboard journey: if it appears, the fixture put the league in the
    /// wrong state, and saying so beats timing out on a leaderboard that was never going to render.
    /// </summary>
    internal const string LeagueNotStarted = "league-not-started";

    /// <summary>
    /// One per account on the admin user list, so several elements share it - which is what makes "the row for
    /// this email" expressible as a filter rather than as an index.
    /// </summary>
    internal const string AdminUserRow = "admin-user-row";

    internal const string AdminUserMenu = "admin-user-menu";

    internal const string AdminUserDelete = "admin-user-delete";

    /// <summary>
    /// The itemised list of what deleting an account destroys, inside the admin confirmation dialog. Lives in
    /// <c>interop.js</c> rather than a component, because the dialog is composed by SweetAlert.
    /// </summary>
    internal const string DeleteUserImpact = "delete-user-impact";

    /// <summary>Shown in place of the list above when the account has no history at all.</summary>
    internal const string DeleteUserImpactEmpty = "delete-user-impact-empty";

    /// <summary>The replacement-administrator picker, shown only when the account administers a league.</summary>
    internal const string DeleteUserNewAdmin = "delete-user-new-admin";

    /// <summary>
    /// The administrator-only "Add Member" flow on a league's members page: open the modal, pick a pass holder,
    /// confirm. Each step is addressable so a failure names the step rather than the journey.
    /// </summary>
    internal const string AddMemberOpen = "add-member-open";

    internal const string AddMemberModal = "add-member-modal";

    internal const string AddMemberSelect = "add-member-select";

    internal const string AddMemberConfirm = "add-member-confirm";

    /// <summary>
    /// Shown in place of the picker when everybody holding a pass for the season is already in the league. Not an
    /// error, which is why it is a distinct element rather than the shared error panel.
    /// </summary>
    internal const string AddMemberNone = "add-member-none";

    /// <summary>
    /// The panel every page shows in place of content when a read behind it fails. One id on the shared
    /// component covers every page at once, which is why "no error anywhere" is cheap to assert.
    /// </summary>
    internal const string ApiError = "api-error";

    /// <summary>
    /// Every id above, for the convention test. Adding a constant without adding it here would leave it
    /// unchecked, so the list is asserted complete by reflection rather than trusted.
    /// </summary>
    internal static readonly IReadOnlyList<string> All =
    [
        LoginEmail,
        LoginPassword,
        LoginSubmit,
        CookieConsent,
        CookieConsentReject,
        NavAccountMenu,
        Dashboard,
        MyLeaguesView,
        JoinPrivateOpen,
        JoinPrivateModal,
        JoinEntryCode,
        JoinContinue,
        JoinPreview,
        JoinConfirm,
        JoinSent,
        OverallLeaderboard,
        LeaderboardRow,
        LeagueNotStarted,
        AdminUserRow,
        AdminUserMenu,
        AdminUserDelete,
        DeleteUserImpact,
        DeleteUserImpactEmpty,
        DeleteUserNewAdmin,
        AddMemberOpen,
        AddMemberModal,
        AddMemberSelect,
        AddMemberConfirm,
        AddMemberNone,
        ApiError
    ];
}

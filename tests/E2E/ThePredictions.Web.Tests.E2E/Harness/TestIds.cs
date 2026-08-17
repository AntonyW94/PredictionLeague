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
        OverallLeaderboard,
        LeaderboardRow,
        LeagueNotStarted,
        ApiError
    ];
}

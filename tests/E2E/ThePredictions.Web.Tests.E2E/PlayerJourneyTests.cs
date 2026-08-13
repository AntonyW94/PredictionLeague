using FluentAssertions;
using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;
using ThePredictions.Web.Tests.E2E.Pages;
using Xunit;

namespace ThePredictions.Web.Tests.E2E;

/// <summary>
/// The settled player: <c>testplayer@dev.local</c>, who holds a Season Pass and belongs to the first league.
///
/// Every assertion here is structural rather than value-based, because dev is refreshed from an anonymised
/// production copy and the data moves underneath the suite. "The leaderboard renders at least one row" holds
/// across a refresh; "row 1 is Antony" does not, and the failure being watched for is the page blowing up
/// rather than the numbers on it being wrong.
/// </summary>
public class PlayerJourneyTests(BrowserFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task Player_ShouldSeeTheirDashboard_WhenTheyLogIn()
    {
        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(E2ESettings.PlayerEmail);

        var dashboard = new PlayerDashboardPage(page);

        await Assertions.Expect(dashboard.Container).ToBeVisibleAsync();

        // The tiles are what prove this is the settled dashboard rather than the onboarding takeover,
        // because the takeover renders the checklist alone and no tiles at all. Deliberately NOT asserted
        // as "no onboarding card": this account has finished the two *required* steps but not the optional
        // ones, so the card is still shown here in its "You're all set" form.
        //
        // If this is what starts failing, look at the account before the page: it means testplayer@dev.local
        // has lost its Season Pass or its league membership, and TestAccountCreator grants both.
        await Assertions.Expect(dashboard.LeaguesHeading.First).ToBeVisibleAsync();
        await Assertions.Expect(dashboard.StandingsHeading.First).ToBeVisibleAsync();
        await Assertions.Expect(dashboard.ErrorMessages).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Player_ShouldSeeALeaderboard_WhenTheyOpenTheirLeague()
    {
        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(E2ESettings.PlayerEmail);
        await new PlayerDashboardPage(page).OpenFirstLeagueAsync();

        var league = new LeagueDashboardPage(page);

        await Assertions.Expect(league.SettledContent.First).ToBeVisibleAsync();

        (await league.PreSeasonNotice.CountAsync()).Should().Be(0,
            "the first league on dev belongs to a season that has not started, so it has no leaderboard to "
            + "render. That is a fixture problem rather than a broken page - TestAccountCreator adds the test "
            + "accounts to the lowest-numbered league, which should be the oldest season on the copy.");

        await Assertions.Expect(league.OverallLeaderboardHeading.First).ToBeVisibleAsync();
        await Assertions.Expect(league.ErrorMessages).ToHaveCountAsync(0);
        await Assertions.Expect(league.LeaderboardRows.First).ToBeVisibleAsync();

        (await league.LeaderboardRows.CountAsync()).Should().BeGreaterThan(0,
            "a leaderboard with no rows means the read behind it returned nothing or failed to materialise, "
            + "which is the 2026-07-30 production failure this journey exists to catch.");
    }

    [Fact]
    public async Task Player_ShouldReturnToTheAnonymousSite_WhenTheyLogOut()
    {
        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(E2ESettings.PlayerEmail);

        var navigation = new NavigationBar(page);
        await navigation.LogOutAsync();

        // The home page renders its signed-out hero, so the Login call to action appearing and the avatar
        // menu disappearing together are what show the session is genuinely gone. Exact, because the
        // competitions rotator carries a "Login to Play" button that a substring match would also find.
        var loginLink = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Login", Exact = true });

        await Assertions.Expect(loginLink.First).ToBeVisibleAsync();

        await Assertions.Expect(navigation.LogoutButton).ToHaveCountAsync(0);
    }
}

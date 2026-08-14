using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;
using ThePredictions.Web.Tests.E2E.Pages;
using Xunit;

namespace ThePredictions.Web.Tests.E2E;

/// <summary>
/// The first journey, and for now the only one: a seeded user signs in and lands on an authenticated
/// dashboard.
///
/// Deliberately alone. The expensive, opinionated part of a browser suite is the stack underneath it - a SQL
/// Server container, production's schema from the committed migrations, the published application launched
/// against it - and none of that is proven until one journey drives it end to end. More journeys come after
/// this passes, not alongside it.
/// </summary>
[Trait(E2ETrait.LevelName, TestLevel.Smoke)]
public class LoginJourneyTests(StackFixture stack) : E2ETestBase(stack)
{
    [Fact]
    public async Task Player_ShouldReachAnAuthenticatedDashboard_WhenTheySignIn()
    {
        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(E2ESettings.PlayerEmail, E2ESettings.PlayerPassword);

        var dashboard = new DashboardPage(page);
        var layout = new SiteLayout(page);

        await Assertions.Expect(dashboard.Container).ToBeVisibleAsync();

        // The URL alone would pass on a page that routed but never authenticated. The avatar menu only
        // renders inside AuthorizeView.Authorized, so its presence is what proves the token came back, was
        // stored, and was read.
        await Assertions.Expect(layout.AccountMenuButton).ToBeVisibleAsync();

        // This user holds no Season Pass and belongs to no league, so what renders is the onboarding
        // takeover rather than the tiles - a legitimate authenticated dashboard, and the one a real new
        // sign-up sees. Asserting no error panel is what makes the difference between "the page rendered"
        // and "the page rendered eight failed reads", which on an empty database is a genuine risk worth
        // catching rather than seeding around.
        await Assertions.Expect(layout.ErrorMessages).ToHaveCountAsync(0);
    }
}

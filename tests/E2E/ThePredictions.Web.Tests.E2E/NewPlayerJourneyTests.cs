using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;
using ThePredictions.Web.Tests.E2E.Pages;
using Xunit;

namespace ThePredictions.Web.Tests.E2E;

/// <summary>
/// The brand-new sign-up: <c>testnewplayer@dev.local</c>, which holds no Season Pass and belongs to no
/// league. Both required onboarding steps are therefore outstanding, which is what puts the dashboard into
/// its takeover shape - a genuinely different page from the settled dashboard, built from a different query,
/// and worth its own journey rather than being the accidental state of the main player account.
/// </summary>
public class NewPlayerJourneyTests(BrowserFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task NewPlayer_ShouldSeeTheOnboardingChecklist_WhenTheyLogIn()
    {
        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(E2ESettings.NewPlayerEmail);

        var dashboard = new PlayerDashboardPage(page);

        await Assertions.Expect(dashboard.Container).ToBeVisibleAsync();
        await Assertions.Expect(dashboard.OnboardingCard.First).ToBeVisibleAsync();

        // At least one step still to do. Counted rather than named: the steps are defined in
        // OnboardingStepRegistry and adding one is expected, so pinning a particular step would make this
        // test fail on a deliberate change rather than on a broken page.
        await Assertions.Expect(dashboard.OutstandingOnboardingSteps.First).ToBeVisibleAsync();

        // No pass means the takeover shows the checklist alone - the tiles only come back once the required
        // steps are done, which is what PlayerJourneyTests asserts from the other side.
        await Assertions.Expect(dashboard.StandingsHeading).ToHaveCountAsync(0);
        await Assertions.Expect(dashboard.ErrorMessages).ToHaveCountAsync(0);
    }
}

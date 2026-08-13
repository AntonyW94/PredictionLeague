using FluentAssertions;
using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;
using ThePredictions.Web.Tests.E2E.Pages;
using Xunit;

namespace ThePredictions.Web.Tests.E2E;

/// <summary>
/// The administrator: <c>testadmin@dev.local</c>. Reaching an admin screen through the gear menu proves both
/// halves of the role - that the JWT carried the Administrator claim far enough for the navigation to offer
/// the menu, and that the page behind its authorisation attribute renders.
/// </summary>
public class AdminJourneyTests(BrowserFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task Admin_ShouldSeeAnAdminScreen_WhenTheyOpenItFromTheNavigation()
    {
        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(E2ESettings.AdminEmail);

        await new NavigationBar(page).GoToManageTeamsAsync();

        var teams = new AdminTeamsPage(page);

        await Assertions.Expect(teams.Heading.First).ToBeVisibleAsync();
        await Assertions.Expect(teams.TeamRows.First).ToBeVisibleAsync();
        await Assertions.Expect(teams.ErrorMessages).ToHaveCountAsync(0);

        (await teams.TeamRows.CountAsync()).Should().BeGreaterThan(0,
            "dev is refreshed from a production copy, so there are always teams to list. None means the read "
            + "behind the page failed rather than that the data ran out.");
    }
}

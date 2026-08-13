using Microsoft.Playwright;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The player's own dashboard at <c>/dashboard</c>. It has two shapes, and which one appears is decided by
/// data rather than by the URL: an account with every required onboarding step done sees the real tiles,
/// while one still missing a Season Pass or a league gets the onboarding takeover instead.
/// </summary>
internal sealed class PlayerDashboardPage(IPage page)
{
    /// <summary>Wraps both shapes, so it is what proves the page itself rendered.</summary>
    internal ILocator Container => page.Locator(".dashboard-container");

    /// <summary>
    /// The My Leagues tile's heading. Exact on purpose: the Available Leagues tile alongside it is headed
    /// "Available Leagues", which a substring match would also find.
    /// </summary>
    internal ILocator LeaguesHeading => page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Leagues", Exact = true });

    internal ILocator StandingsHeading => page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Standings", Exact = true });

    internal ILocator OnboardingCard => page.Locator(".onboarding-card");

    internal ILocator OutstandingOnboardingSteps => page.Locator(".onboarding-step--active");

    internal ILocator ErrorMessages => page.Locator(".message-box-solid.error");

    /// <summary>
    /// Opens the first league on the My Leagues carousel. The button is labelled "View Dashboard" for a
    /// league in progress and "View Recap" for a finished one, so it is located by its position in the card
    /// actions rather than by its text - which season dev happens to hold is not something a smoke test
    /// should care about.
    /// </summary>
    internal async Task OpenFirstLeagueAsync()
    {
        var viewButton = page.Locator(".my-leagues-card-actions .purple-accent-button").First;

        await Assertions.Expect(viewButton).ToBeVisibleAsync();
        await viewButton.ClickAsync();

        await page.WaitForURLAsync(url =>
            url.Contains("/leagues/", StringComparison.Ordinal)
            && url.EndsWith("/dashboard", StringComparison.Ordinal));
    }
}

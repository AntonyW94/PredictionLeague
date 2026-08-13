using Microsoft.Playwright;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The signed-in navigation bar: the avatar menu every account has, and the gear menu only an administrator
/// sees.
/// </summary>
internal sealed class NavigationBar(IPage page)
{
    private ILocator AvatarMenuButton => page.Locator(".nav-avatar-btn");

    private ILocator AdminMenuButton => page.Locator(".nav-icon-btn");

    internal ILocator LogoutButton => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Log out" });

    /// <summary>Opens the gear menu and follows its Manage Teams link.</summary>
    internal async Task GoToManageTeamsAsync()
    {
        await AdminMenuButton.ClickAsync();

        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Manage Teams" }).ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/admin/teams", StringComparison.Ordinal));
    }

    /// <summary>
    /// Signs out through the avatar menu and waits for the anonymous home page, which is where the client
    /// force-loads to once the session is gone.
    /// </summary>
    internal async Task LogOutAsync()
    {
        await AvatarMenuButton.ClickAsync();

        await Assertions.Expect(LogoutButton).ToBeVisibleAsync();
        await LogoutButton.ClickAsync();

        await page.WaitForURLAsync(url => !url.Contains("/dashboard", StringComparison.Ordinal));
    }
}

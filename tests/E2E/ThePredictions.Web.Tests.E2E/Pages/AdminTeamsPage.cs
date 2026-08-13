using Microsoft.Playwright;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The Manage Teams screen at <c>/admin/teams</c>, chosen as the admin smoke target because it is guarded by
/// <c>[Authorize(Roles = RoleNames.Administrator)]</c> and lists rows from a production copy without needing
/// any particular round or season state.
/// </summary>
internal sealed class AdminTeamsPage(IPage page)
{
    internal ILocator Heading =>
        page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Manage Teams", Exact = true });

    internal ILocator TeamRows => page.Locator(".team-list-grid .team-list-row");

    internal ILocator ErrorMessages => page.Locator(".message-box-solid.error");
}

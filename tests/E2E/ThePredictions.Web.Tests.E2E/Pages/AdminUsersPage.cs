using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The administrator's account list at <c>/admin/users</c>, and the delete flow that hangs off each row.
/// </summary>
/// <remarks>
/// <para>
/// The confirmation dialog is SweetAlert, which composes its own markup, so the parts we author carry a
/// <c>data-test-id</c> from <c>interop.js</c> and are addressed the usual way. Its <b>buttons</b> cannot be
/// annotated - SweetAlert builds them itself and takes only a class name - so those are addressed by
/// <c>.swal2-confirm</c> and <c>.swal2-cancel</c>. That is a documented part of SweetAlert's own API rather
/// than one of our styling hooks, which is what makes it a stable contract and not the thing
/// <see cref="TestIds"/> exists to avoid.
/// </para>
/// </remarks>
internal sealed class AdminUsersPage(IPage page)
{
    private ILocator Rows => page.GetByTestId(TestIds.AdminUserRow);

    /// <summary>The itemised "this will permanently delete" list inside the confirmation dialog.</summary>
    internal ILocator DeletionImpact => page.GetByTestId(TestIds.DeleteUserImpact);

    /// <summary>What the dialog shows instead, when the account holds nothing at all.</summary>
    internal ILocator DeletionImpactEmpty => page.GetByTestId(TestIds.DeleteUserImpactEmpty);

    /// <summary>The replacement-administrator picker, present only when the account administers a league.</summary>
    internal ILocator NewAdministratorSelect => page.GetByTestId(TestIds.DeleteUserNewAdmin);

    private ILocator ConfirmButton => page.Locator(".swal2-confirm");

    private ILocator CancelButton => page.Locator(".swal2-cancel");

    internal async Task GoToAsync()
    {
        await page.GotoAsync("/admin/users");

        // The navigation timeout, not the assertion one: this waits for the list's own read as well as for
        // whatever of the Blazor runtime is not already warm.
        await Rows.First.ShouldBeVisibleAsync(E2ESettings.NavigationTimeoutMs);
    }

    /// <summary>
    /// The row for one account, found by the address it displays rather than by position - the list is sorted
    /// by name and seeded alongside other classes' fixtures, so an index would be a guess.
    /// </summary>
    internal ILocator RowFor(string email) => Rows.Filter(new LocatorFilterOptions { HasText = email });

    /// <summary>
    /// Opens the delete confirmation for one account and waits for it to be readable, without confirming.
    /// </summary>
    internal async Task OpenDeleteDialogAsync(string email)
    {
        var row = RowFor(email);

        await row.GetByTestId(TestIds.AdminUserMenu).ClickAsync();
        await row.GetByTestId(TestIds.AdminUserDelete).ClickAsync();

        // The dialog cannot draw its contents until the impact read comes back, so waiting on the button is
        // what separates "the dialog opened" from "the dialog has something to say".
        await ConfirmButton.ShouldBeVisibleAsync();
    }

    internal async Task ConfirmAsync() => await ConfirmButton.ClickAsync();

    internal async Task CancelAsync() => await CancelButton.ClickAsync();
}

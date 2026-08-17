using Microsoft.Playwright;
using ThePredictions.Web.Tests.E2E.Harness;

namespace ThePredictions.Web.Tests.E2E.Pages;

/// <summary>
/// The join-a-private-league-by-code modal, opened from the Available Leagues tile on the dashboard.
/// </summary>
/// <remarks>
/// Two steps, and they call different endpoints, which is why each is addressable separately: entering the
/// code fetches a preview (<c>GET</c> the join preview by code), and confirming actually joins
/// (<c>POST /api/leagues/join</c>). A journey that only asserted the end state could not say which of those
/// failed.
/// </remarks>
internal sealed class JoinPrivateLeagueModal(IPage page)
{
    internal ILocator OpenButton => page.GetByTestId(TestIds.JoinPrivateOpen);

    internal ILocator Modal => page.GetByTestId(TestIds.JoinPrivateModal);

    internal ILocator EntryCodeField => page.GetByTestId(TestIds.JoinEntryCode);

    internal ILocator ContinueButton => page.GetByTestId(TestIds.JoinContinue);

    /// <summary>The league summary shown once the code has been resolved. Step one succeeded if this appears.</summary>
    internal ILocator Preview => page.GetByTestId(TestIds.JoinPreview);

    internal ILocator ConfirmButton => page.GetByTestId(TestIds.JoinConfirm);

    /// <summary>The "request has been sent" confirmation. Step two succeeded if this appears.</summary>
    internal ILocator SentConfirmation => page.GetByTestId(TestIds.JoinSent);

    /// <summary>
    /// Opens the modal from the dashboard. The button only renders when the dashboard's private-league check
    /// says one is available, so this failing means the fixture is wrong rather than the flow.
    /// </summary>
    internal async Task OpenAsync()
    {
        await OpenButton.ShouldBeVisibleAsync();
        await OpenButton.ClickAsync();

        await EntryCodeField.ShouldBeVisibleAsync();
    }

    /// <summary>Step one: enter the code and resolve it to a league.</summary>
    internal async Task EnterCodeAsync(string entryCode)
    {
        await EntryCodeField.FillAsync(entryCode);
        await ContinueButton.ClickAsync();
    }
}

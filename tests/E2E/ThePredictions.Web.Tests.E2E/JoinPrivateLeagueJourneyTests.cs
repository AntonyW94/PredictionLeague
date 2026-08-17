using ThePredictions.Web.Tests.E2E.Harness;
using ThePredictions.Web.Tests.E2E.Pages;
using Xunit;

namespace ThePredictions.Web.Tests.E2E;

/// <summary>
/// Joining a private league with its entry code - reported broken in production in August 2026, while joining
/// a free public league works.
/// </summary>
/// <remarks>
/// <para>
/// Written to the <b>intended</b> behaviour, before any fix, and expected to fail. That order matters: a test
/// written after a fix can pass for the wrong reason, and would not have proved it reproduced the fault at
/// all. When this goes green, the fault is fixed - and it stays fixed.
/// </para>
/// <para>
/// The two paths differ more than the description suggests, which is consistent with one working and the other
/// not. Public join is <c>POST /api/leagues/{leagueId}/join</c> straight from the tile. Private join is two
/// separate calls: resolve the code to a preview, then <c>POST /api/leagues/join</c> with the code. So the
/// assertions below step through the flow rather than only checking the end, and the failing assertion names
/// which half is broken.
/// </para>
/// <para>
/// Reading the server path first ruled out the obvious candidates:
/// <c>JoinLeagueRequestValidator</c> accepts six alphanumeric characters case-insensitively;
/// <c>GetByEntryCodeAsync</c> is a plain equality against a case-insensitive collation; and the
/// <c>Guard.Against.EntityNotFound(request.LeagueId ?? 0, ...)</c> in <c>JoinLeagueCommandHandler</c> only
/// throws on a null entity, so passing 0 on the code path makes its <i>message</i> wrong ("League with key 0")
/// without changing behaviour. Worth tidying, but not this fault.
/// </para>
/// </remarks>
[Trait(E2ETrait.LevelName, TestLevel.Core)]
public class JoinPrivateLeagueJourneyTests(StackFixture stack) : E2ETestBase(stack), IAsyncLifetime
{
    private SeededPrivateLeague _league = null!;

    public async ValueTask InitializeAsync() =>
        _league = await Database.SeedPrivateLeagueToJoinAsync(nameof(JoinPrivateLeagueJourneyTests));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Player_ShouldJoinAPrivateLeague_WhenTheyEnterItsEntryCode()
    {
        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(_league.PlayerEmail, _league.PlayerPassword);

        var dashboard = new DashboardPage(page);
        var layout = new SiteLayout(page);
        var join = new JoinPrivateLeagueModal(page);

        await dashboard.Container.ShouldBeVisibleAsync();

        // The button only renders when the dashboard's private-leagues check finds one available - a NOT
        // EXISTS against LeagueMembers plus a Season Pass for the season. If this is what fails, the fixture
        // is wrong rather than the flow.
        await join.OpenAsync();

        // Step one: resolve the code. A failure here is the preview lookup, not the join.
        await join.EnterCodeAsync(_league.EntryCode);

        await join.Preview.ShouldBeVisibleAsync();

        // Nothing should have gone wrong resolving a code that exists, holds a pass, and is not already
        // joined - so an error panel at this point IS the bug, and catching it here says which step.
        await layout.ErrorMessages.ShouldReportNoErrorsAsync();

        // Step two: actually join. The league requires approval, so success is a request sent rather than
        // immediate membership.
        await join.ConfirmButton.ShouldBeVisibleAsync();
        await join.ConfirmButton.ClickAsync();

        await join.SentConfirmation.ShouldBeVisibleAsync();

        await layout.ErrorMessages.ShouldReportNoErrorsAsync();
    }
}

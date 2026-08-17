using AwesomeAssertions;
using ThePredictions.Web.Tests.E2E.Harness;
using ThePredictions.Web.Tests.E2E.Pages;
using Xunit;

namespace ThePredictions.Web.Tests.E2E;

/// <summary>
/// An administrator deletes an account that holds a season pass, and is told what that destroys first.
/// </summary>
/// <remarks>
/// <para>
/// This journey exists because of a production failure that nothing else in the suite could have caught.
/// Deleting a real account - no leagues created, none joined, one season pass - returned "An internal server
/// error has occurred." The cause was in the schema rather than in any code: eleven foreign keys pointed at
/// <c>[AspNetUsers]</c> without <c>ON DELETE CASCADE</c>, so the single-statement delete failed with error
/// 547 and the <c>SqlException</c> fell through to the unhandled bucket. The handler's unit tests mock
/// <c>IUserManager</c> and passed throughout.
/// </para>
/// <para>
/// <c>UserDeletionCascadeTests</c> in the integration suite pins the schema itself and is the faster,
/// narrower guard - it fails in seconds and names the constraint. This is the wider one: it proves the whole
/// path an administrator actually takes, including that the dialog tells them what they are about to destroy
/// rather than presenting a bare "cannot be undone".
/// </para>
/// <para>
/// <see cref="TestLevel.Extended"/>, and the first class at that level. Account deletion is rare - an
/// administrator does it a handful of times a year - so it does not belong in the per-commit run, but it is
/// destructive and irreversible, which is exactly why it has to work when it is reached.
/// </para>
/// </remarks>
[Trait(E2ETrait.LevelName, TestLevel.Extended)]
public class DeleteUserJourneyTests(StackFixture stack) : E2ETestBase(stack)
{
    /// <summary>
    /// A fresh administrator and target account for the calling test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per <b>test</b>, not per class, and that is the difference from every other journey here. xUnit builds
    /// a new instance of a test class for each test it runs, so an <c>IAsyncLifetime.InitializeAsync</c> that
    /// seeds runs once per test rather than once per class - which the existing single-test classes never
    /// revealed. Seeding one scope from there made the second test fail on a duplicate competition code.
    /// </para>
    /// <para>
    /// It also has to be per test for a second reason: the first test <i>deletes</i> the account, so a shared
    /// target would leave the other test with nothing to find.
    /// </para>
    /// <para>
    /// Not in the fixture either, because the administrator needs an Identity role and those are written by
    /// <c>DatabaseInitialiser</c> at application startup - later than the fixture seeds, earlier than this.
    /// </para>
    /// </remarks>
    private async Task<SeededDeletableUser> SeedForAsync(string testScope) =>
        await Database.SeedDeletableUserAsync($"{nameof(DeleteUserJourneyTests)}{testScope}");

    [Fact]
    public async Task Administrator_ShouldSeeWhatWillBeDeleted_WhenTheyOpenTheConfirmation()
    {
        var seeded = await SeedForAsync("Preview");

        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(seeded.AdminEmail, seeded.AdminPassword);

        var users = new AdminUsersPage(page);
        await users.GoToAsync();
        await users.OpenDeleteDialogAsync(seeded.TargetEmail);

        // The itemised list is the point of the dialog: "this action cannot be undone" does not tell an
        // administrator that they are about to destroy a purchase record.
        await users.DeletionImpact.ShouldBeVisibleAsync();
        await users.DeletionImpact.ShouldContainTextAsync("season pass");

        // No reassignment step, because this account administers nothing - and a picker appearing here would
        // mean the impact read miscounted the leagues rather than that the dialog is merely untidy.
        await users.NewAdministratorSelect.ShouldNotExistAsync();
        await users.DeletionImpactEmpty.ShouldNotExistAsync();

        // Nothing has been confirmed, so nothing may have happened yet.
        await users.CancelAsync();
        (await Database.UserExistsAsync(seeded.TargetUserId)).Should().BeTrue(
            "the dialog was cancelled, so the account is still there.");
    }

    [Fact]
    public async Task Administrator_ShouldDeleteTheAccount_WhenItHoldsASeasonPass()
    {
        var seeded = await SeedForAsync("Delete");

        await using var session = await StartSessionAsync();
        var page = session.Page;

        await new LoginPage(page).SignInAsync(seeded.AdminEmail, seeded.AdminPassword);

        var users = new AdminUsersPage(page);
        var layout = new SiteLayout(page);

        await users.GoToAsync();
        await users.OpenDeleteDialogAsync(seeded.TargetEmail);
        await users.ConfirmAsync();

        // The row going is what an administrator sees succeed. Before 0009 this is where the journey failed:
        // the row stayed, and an error panel appeared carrying the internal-server-error message.
        await users.RowFor(seeded.TargetEmail).ShouldNotExistAsync();
        await layout.ErrorMessages.ShouldReportNoErrorsAsync();

        // ...and the database is what proves it was a delete rather than a list that merely refreshed.
        (await Database.UserExistsAsync(seeded.TargetUserId)).Should().BeFalse(
            "confirming the dialog deletes the account, season pass and all.");
    }
}

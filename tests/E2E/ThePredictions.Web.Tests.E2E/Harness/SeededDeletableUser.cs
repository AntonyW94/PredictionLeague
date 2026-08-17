namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// What <c>TestDatabase.SeedDeletableUserAsync</c> created: an administrator who can sign in and reach the
/// user list, and an ordinary account for them to delete.
/// </summary>
/// <param name="TargetEmail">
/// How the journey finds the right row. The list shows the address, so this is what a row filter matches -
/// and it is unique to the seeding test class, which is what keeps classes from deleting each other's
/// fixtures.
/// </param>
/// <param name="TargetFullName">As the list renders it, for asserting the dialog is about the right person.</param>
internal sealed record SeededDeletableUser(
    string AdminEmail,
    string AdminPassword,
    string TargetUserId,
    string TargetEmail,
    string TargetFullName);

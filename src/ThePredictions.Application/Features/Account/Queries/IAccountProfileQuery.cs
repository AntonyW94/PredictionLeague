namespace ThePredictions.Application.Features.Account.Queries;

/// <summary>Reads a player's own details, or nothing if there is no such account.</summary>
/// <remarks>
/// The marketing opt-in arrives as the date it happened rather than a yes-or-no. Reading "there is a date, so they said yes"
/// is a rule, and it was a <c>CASE WHEN ... IS NOT NULL</c> in the statement.
/// </remarks>
public interface IAccountProfileQuery
{
    Task<AccountProfileRow?> ExecuteAsync(string userId, CancellationToken cancellationToken);
}

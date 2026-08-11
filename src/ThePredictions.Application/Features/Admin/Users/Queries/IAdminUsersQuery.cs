namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// Reads every account for the administrator's user list, with the memberships, passes and winnings the figures on that
/// screen are worked out from.
/// </summary>
/// <remarks>
/// Five sets rather than one row per account with eleven correlated subqueries hanging off it. What each of those
/// subqueries computed - how much someone has spent, how many leagues they are in, whether they hold a pass - is a rule
/// about what counts, and three of them had a definition inside the <c>WHERE</c> clause that no screen or test ever
/// stated. Forty-four accounts and a few hundred rows in total.
/// </remarks>
public interface IAdminUsersQuery
{
    Task<AdminUsersData> ExecuteAsync(CancellationToken cancellationToken);
}

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// Reads the leagues the player administers, and every request waiting for their decision.
/// </summary>
/// <remarks>
/// Every league they administer, open or closed, and every pending request to any of them. Which leagues still count as open
/// - and therefore which requests are actionable - is a rule and lives in the handler.
/// </remarks>
public interface IAdminPendingMembersQuery
{
    Task<AdminPendingMembersData> ExecuteAsync(string userId, CancellationToken cancellationToken);
}

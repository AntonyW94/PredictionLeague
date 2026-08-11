namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// Reads every membership the player has that is not yet an approved one - a request waiting on an administrator, or a
/// request that was turned down.
/// </summary>
/// <remarks>
/// Rejections are returned whether or not the player has dismissed the notice about them. Which ones are still worth showing
/// is a rule, and it is the handler's.
/// </remarks>
public interface IMyLeagueRequestsQuery
{
    Task<IReadOnlyList<MyLeagueRequestRow>> ExecuteAsync(string userId, CancellationToken cancellationToken);
}

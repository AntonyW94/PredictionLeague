namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>
/// Counts what deleting one account would destroy, so the confirmation dialog can say so before the
/// administrator commits to it.
/// </summary>
public interface IUserDeletionImpactQuery
{
    Task<UserDeletionImpactRow> ExecuteAsync(string userId, CancellationToken cancellationToken);
}

namespace ThePredictions.Application.Features.Badges.Queries;

/// <summary>
/// Reads everything one player's badges screen and dashboard tile are built from: the badges they hold, and the
/// raw material behind the ones they do not.
/// </summary>
/// <remarks>
/// Nothing about badge progress is stored, so all of it used to be computed by the database - six statements
/// including two gap-and-island streak queries. None of that survives here. What comes back is rounds and awards;
/// what they add up to is <see cref="BadgeState"/>'s job.
/// </remarks>
public interface IBadgeStateQuery
{
    Task<BadgeStateData> ExecuteAsync(string userId, CancellationToken cancellationToken);
}

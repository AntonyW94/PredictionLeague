namespace ThePredictions.Application.Services;

public interface ISeasonAccessService
{
    /// <summary>
    /// Ensures the user may take part in the given season (join or create a league in it),
    /// applying the Season Pass access rule and granting a free trial / recording free
    /// participation where appropriate. Throws <see cref="Domain.Common.Exceptions.SeasonPassRequiredException"/>
    /// when a pass is required but the user is not entitled.
    /// </summary>
    Task EnsureCanParticipateAsync(string userId, int seasonId, CancellationToken cancellationToken);
}

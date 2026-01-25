namespace PredictionLeague.Application.Services;

/// <summary>
/// Provides access to the current authenticated user's information.
/// Returns null values when no user is authenticated (e.g., API key auth).
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's ID from the authentication context.
    /// Returns null if no user is authenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets whether a user is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Checks if the current user is in the Administrator role.
    /// Returns false if no user is authenticated.
    /// </summary>
    bool IsAdministrator { get; }

    /// <summary>
    /// Ensures the current user is authenticated and is an administrator.
    /// Throws UnauthorizedAccessException if not.
    /// </summary>
    void EnsureAdministrator();
}

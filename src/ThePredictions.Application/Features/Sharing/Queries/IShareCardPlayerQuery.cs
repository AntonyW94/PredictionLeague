namespace ThePredictions.Application.Features.Sharing.Queries;

/// <summary>
/// Reads the player the share card is being made for: their first name and the theme they have chosen, or nothing if there is
/// no such account.
/// </summary>
/// <remarks>
/// The statement this replaces reached the player by joining <c>[AspNetUsers]</c> onto the round with no relationship between
/// them, purely to fetch two columns in the same trip. Whether a blank name means "show no name", and how the saved theme
/// interacts with the one the browser asks for, are both rules.
/// </remarks>
public interface IShareCardPlayerQuery
{
    Task<ShareCardPlayerRow?> ExecuteAsync(string userId, CancellationToken cancellationToken);
}

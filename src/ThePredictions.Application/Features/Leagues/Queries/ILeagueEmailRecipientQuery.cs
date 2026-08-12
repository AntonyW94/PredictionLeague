namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads who to email about a league and which season it is for, or nothing if either is missing.
/// </summary>
/// <remarks>
/// One read behind two notification handlers - "your request to join was approved" and "somebody wants to join your league" -
/// which had the identical statement each, a <c>CROSS JOIN</c> of a player against a season with no relationship between them,
/// used to fetch four columns in one trip.
/// </remarks>
public interface ILeagueEmailRecipientQuery
{
    Task<LeagueEmailRecipientRow?> ExecuteAsync(string userId, int seasonId, CancellationToken cancellationToken);
}

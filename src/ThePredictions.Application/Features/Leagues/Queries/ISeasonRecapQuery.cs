namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads one player's season in one league: the league's members and every score they posted, plus the two things
/// only that player has (their prize money and their exact scores).
///
/// Returns <c>null</c> when the league does not exist.
/// </summary>
/// <remarks>
/// Scoped to one player by contract, which is why some of what it returns is league-wide and some is not. The
/// player's final position and the rounds they won can only be worked out against everyone else, so scores and
/// members come back whole; their winnings and exact scores are theirs alone and nothing on the recap compares
/// them with anyone.
/// </remarks>
public interface ISeasonRecapQuery
{
    Task<SeasonRecapData?> ExecuteAsync(int leagueId, string userId, CancellationToken cancellationToken);
}

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// Reads the rounds that could appear on a player's dashboard, and the matches in them.
/// </summary>
/// <remarks>
/// "Could appear" is as far as the read goes: rounds of an active season the player has a league in, excluding drafts and
/// finished rounds. Whether a round is still worth showing depends on when its last match locks and on whether the player has
/// predicted, and both of those are rules.
///
/// The matches exclude postponed ones, which is what the old statement did. That is not only presentation - a postponed match
/// must not hold a round open, and the deadline rule is computed from these rows.
/// </remarks>
public interface IActiveRoundsQuery
{
    Task<ActiveRoundsData> ExecuteAsync(string userId, CancellationToken cancellationToken);
}

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// Reads everything the round-results email is built from: the season's rounds, who was scored in this one, the leagues
/// they belong to, and what everybody scored in each league.
/// </summary>
/// <remarks>
/// Four sets rather than one wide row per player and league. The statement this replaces joined six tables and two CTEs
/// into a single flat result, which meant every player-level column repeated down their league rows and the mapping had
/// to collapse them again on the way out.
///
/// Nothing here is filtered by whether a player should receive the email, and nothing is ranked. Who takes part, who
/// tops each league, how far each player has moved and which round comes next are all rules.
/// </remarks>
public interface IRoundDigestQuery
{
    Task<RoundDigestData> ExecuteAsync(int roundId, CancellationToken cancellationToken);
}

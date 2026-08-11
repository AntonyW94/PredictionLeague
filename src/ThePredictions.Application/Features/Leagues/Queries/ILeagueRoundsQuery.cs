namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads every round of the season a league is playing, with each round's fixture count.
/// </summary>
/// <remarks>
/// Serves the league dashboard and the dashboard's round picker. Both listed a league's rounds with the same eight
/// columns and the same correlated fixture count; they differ only in which of those rounds they keep, and that is a
/// rule each applies for itself.
/// </remarks>
public interface ILeagueRoundsQuery
{
    Task<IReadOnlyList<LeagueRoundRow>> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}

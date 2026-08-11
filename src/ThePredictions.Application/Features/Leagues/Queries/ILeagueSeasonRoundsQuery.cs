namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads every round of the season a league is playing, with the tournament stage each round belongs to.
/// </summary>
/// <remarks>
/// Serves both league pickers - the months and the stages. They asked for the same rows from the same table and
/// differed only in what they grouped by, so one read now answers both and the grouping is a rule in each handler.
/// </remarks>
public interface ILeagueSeasonRoundsQuery
{
    Task<IReadOnlyList<LeagueSeasonRoundRow>> ExecuteAsync(int leagueId, CancellationToken cancellationToken);
}

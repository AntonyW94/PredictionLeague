namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// Reads the facts behind one calendar month's leaderboard for a league.
/// </summary>
/// <remarks>
/// The month is matched on the calendar month alone, within the league's season - not month and year. That is
/// sound only because a season never spans the same month twice (they run August to May), and it is the
/// behaviour being preserved rather than a choice made here.
/// </remarks>
public interface IMonthlyLeaderboardQuery
{
    Task<MonthlyLeaderboardData> ExecuteAsync(int leagueId, int month, CancellationToken cancellationToken);
}

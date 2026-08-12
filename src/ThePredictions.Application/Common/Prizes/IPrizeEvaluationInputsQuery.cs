namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Reads what a prize evaluation needs about one league: the pot context, the season's shape, and the prize scheme if it has one.
/// </summary>
/// <remarks>
/// Two entry points because a league is reached two ways - by id from its own pages, and by entry code from the join flow. Those
/// were the same projection with a predicate concatenated onto the end of it at run time, which is the one thing
/// <c>ThePredictions.SchemaCheck</c> cannot verify: it can only describe a statement that exists as a constant.
/// </remarks>
public interface IPrizeEvaluationInputsQuery
{
    Task<PrizeEvaluationInputsData?> GetByLeagueIdAsync(int leagueId, CancellationToken cancellationToken);

    Task<PrizeEvaluationInputsData?> GetByEntryCodeAsync(string entryCode, CancellationToken cancellationToken);
}

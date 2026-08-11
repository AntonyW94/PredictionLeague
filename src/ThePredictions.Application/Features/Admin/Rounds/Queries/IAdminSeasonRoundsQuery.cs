namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>Reads every round of one season for the administrator's round list.</summary>
/// <remarks>
/// In no order - the list is shown by round number, and that is the handler's rule.
///
/// What is gone from the statement is a whole CTE. It counted the season's approved league members, cross-joined the
/// single row it produced onto every round, and then selected none of it. The same dead count appeared in the round
/// detail statement, so it was written twice and read nowhere.
/// </remarks>
public interface IAdminSeasonRoundsQuery
{
    Task<IReadOnlyList<AdminRoundRow>> ExecuteAsync(int seasonId, CancellationToken cancellationToken);
}

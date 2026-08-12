namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>Reads what one player predicted in one round.</summary>
/// <remarks>
/// Shared by the prediction page, which fills the form in, and the share card, which shows how it went. Both used to reach
/// the predictions through a join in their own statement - the page through a left join to keep unpredicted fixtures, the
/// card through an inner join to drop them, which is a rule about what each screen is for.
/// </remarks>
public interface IUserRoundPredictionsQuery
{
    Task<IReadOnlyList<UserRoundPredictionRow>> ExecuteAsync(string userId, int roundId, CancellationToken cancellationToken);
}

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// Reads every prize won in a round's season, the notifications already sent about them, and the season's round names.
/// </summary>
/// <remarks>
/// The statement this replaces matched a winning against the sent-log with
/// <c>ISNULL(pn.[RoundNumber], -1) = ISNULL(w.[RoundNumber], -1)</c> on both the round and the month, because SQL will not
/// treat two nulls as equal. That sentinel is a workaround for a dialect rather than a rule about prizes, and in C# the
/// comparison it was imitating is the natural one.
/// </remarks>
public interface IPrizeWinnersQuery
{
    Task<PrizeWinnersData> ExecuteAsync(int roundId, CancellationToken cancellationToken);
}

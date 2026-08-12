using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Services;

/// <summary>
/// Recalculates and stores every player's outcome tally for a round.
/// </summary>
/// <remarks>
/// Two commands need this - processing a round's results, and recalculating a whole season - so the sequence lives here
/// rather than twice. Both already hold the round, which is where the fixtures come from.
/// </remarks>
public interface IRoundResultsService
{
    Task RecalculateAsync(Round round, CancellationToken cancellationToken);
}

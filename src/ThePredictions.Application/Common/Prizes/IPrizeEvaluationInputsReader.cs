namespace ThePredictions.Application.Common.Prizes;

/// <summary>Loads the live prize-evaluation inputs for a league from the read side (CQRS query path).</summary>
public interface IPrizeEvaluationInputsReader
{
    Task<PrizeEvaluationInputs?> LoadAsync(int leagueId, CancellationToken cancellationToken);

    /// <summary>Loads inputs by a league's private entry code (used by the prospective-join preview).</summary>
    Task<PrizeEvaluationInputs?> LoadByEntryCodeAsync(string entryCode, CancellationToken cancellationToken);
}

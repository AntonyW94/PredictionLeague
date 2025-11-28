namespace PredictionLeague.Contracts.Boosts;

public sealed class ApplyBoostResultDto
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool AlreadyUsedThisRound { get; init; }
}
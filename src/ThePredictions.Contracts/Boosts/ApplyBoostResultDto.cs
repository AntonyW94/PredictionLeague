using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage]
public sealed class ApplyBoostResultDto
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool AlreadyUsedThisRound { get; init; }
}

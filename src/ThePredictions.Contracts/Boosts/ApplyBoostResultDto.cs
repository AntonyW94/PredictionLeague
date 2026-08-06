using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public sealed class ApplyBoostResultDto
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool AlreadyUsedThisRound { get; init; }
}

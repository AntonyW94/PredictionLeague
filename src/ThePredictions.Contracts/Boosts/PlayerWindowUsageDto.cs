using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage]
public sealed class PlayerWindowUsageDto
{
    public string UserId { get; init; } = string.Empty;
    public string PlayerName { get; init; } = string.Empty;
    public int Remaining { get; init; }
    public int MaxUses { get; init; }
    public bool IsCurrentUser { get; init; }
    public List<BoostUsageDetailDto> Usages { get; init; } = [];
}

using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class DefinePrizeStructureRequest
{
    public List<DefinePrizeSettingDto> PrizeSettings { get; init; } = [];
}

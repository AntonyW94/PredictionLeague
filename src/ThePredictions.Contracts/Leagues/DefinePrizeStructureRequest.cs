using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public class DefinePrizeStructureRequest
{
    public List<DefinePrizeSettingDto> PrizeSettings { get; init; } = [];
}

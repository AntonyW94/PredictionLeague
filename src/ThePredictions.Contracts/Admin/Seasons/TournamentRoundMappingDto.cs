using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Seasons;

[ExcludeFromCodeCoverage]
public class TournamentRoundMappingDto
{
    public int RoundNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public List<TournamentStage> Stages { get; set; } = [];
    public int ExpectedMatchCount { get; set; }
}

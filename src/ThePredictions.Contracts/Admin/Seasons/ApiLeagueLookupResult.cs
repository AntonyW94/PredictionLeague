using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Seasons;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class ApiLeagueLookupResult
{
    public string? LeagueName { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public int RoundCount { get; set; }
    public int TeamCount { get; set; }
    public CompetitionType CompetitionType { get; set; }
    public List<TournamentStage> TournamentStages { get; set; } = [];
}

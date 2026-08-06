using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class ManageLeaguesDto
{
    public List<LeagueDto> PublicLeagues { get; set; } = [];
    public List<LeagueDto> MyPrivateLeagues { get; set; } = [];
    public List<LeagueDto> OtherPrivateLeagues { get; set; } = [];
}

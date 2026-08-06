using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Competitions;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class BaseCompetitionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public int? ApiLeagueId { get; set; }
}

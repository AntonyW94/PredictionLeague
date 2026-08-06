using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Teams;

[ExcludeFromCodeCoverage]
public class BaseTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public int? ApiTeamId { get; set; }
}

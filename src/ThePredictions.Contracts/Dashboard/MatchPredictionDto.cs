using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Dashboard;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage]
public class MatchPredictionDto
{
    public int MatchId { get; init; }
    public DateTime MatchDateTimeUtc { get; init; }
    public int? MatchNumber { get; init; }
    public string? HomeTeamName { get; init; }
    public string? HomeTeamShortName { get; init; }
    public string? HomeTeamAbbreviation { get; init; }
    public string? HomeTeamLogoUrl { get; init; }
    public string? AwayTeamName { get; init; }
    public string? AwayTeamShortName { get; init; }
    public string? AwayTeamAbbreviation { get; init; }
    public string? AwayTeamLogoUrl { get; init; }
    public string? PlaceholderHomeName { get; init; }
    public string? PlaceholderAwayName { get; init; }
    public bool AreTeamsConfirmed { get; init; }
    public DateTime? CustomLockTimeUtc { get; init; }
    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }
}

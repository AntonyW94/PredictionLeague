namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// Lightweight DTO for displaying match predictions on the dashboard upcoming rounds tile.
/// Contains only the data needed for the compact match preview (logos and scores).
/// </summary>
public record UpcomingMatchDto(
    int MatchId,
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    int? PredictedHomeScore,
    int? PredictedAwayScore
);

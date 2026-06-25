using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Dashboard;

/// <summary>
/// Lightweight DTO for displaying match predictions on the dashboard active rounds tile.
/// Contains only the data needed for the compact match preview (logos, predicted score,
/// outcome, the actual match score for in-progress and completed matches, and the kickoff
/// time so scheduled matches can show when they start). Tournament rounds can also carry
/// placeholder (TBC) fixtures whose teams are not yet known - these have no logos and are
/// shown using their placeholder match name instead.
/// </summary>
public record ActiveRoundMatchDto(
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    PredictionOutcome? Outcome,
    MatchStatus Status,
    int? ActualHomeScore,
    int? ActualAwayScore,
    DateTime MatchDateTimeUtc,
    int? MatchNumber,
    bool AreTeamsConfirmed,
    string? PlaceholderHomeName,
    string? PlaceholderAwayName
);

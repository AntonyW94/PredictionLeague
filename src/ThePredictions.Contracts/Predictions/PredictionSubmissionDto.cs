using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Predictions;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record PredictionSubmissionDto(
    int MatchId,
    int HomeScore,
    int AwayScore
);

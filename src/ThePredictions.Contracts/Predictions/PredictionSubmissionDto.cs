using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Predictions;

[ExcludeFromCodeCoverage]
public record PredictionSubmissionDto(
    int MatchId,
    int HomeScore,
    int AwayScore
);

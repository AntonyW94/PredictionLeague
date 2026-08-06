using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record MonthDto(int Month, string Name, int RoundsRemaining, int RoundsCompleted);

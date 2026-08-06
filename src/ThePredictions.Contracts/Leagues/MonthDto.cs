using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public record MonthDto(int Month, string Name, int RoundsRemaining, int RoundsCompleted);

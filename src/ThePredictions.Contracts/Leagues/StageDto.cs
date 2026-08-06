using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public record StageDto(TournamentStageGroup Stage, string Name, int RoundsRemaining, int RoundsCompleted);

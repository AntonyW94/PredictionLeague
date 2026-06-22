using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

public record StageDto(TournamentStageGroup Stage, string Name, int RoundsRemaining, int RoundsCompleted);

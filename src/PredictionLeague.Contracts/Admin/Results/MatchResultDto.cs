using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Admin.Results;

public record MatchResultDto(int MatchId, int HomeScore, int AwayScore, MatchStatus Status);
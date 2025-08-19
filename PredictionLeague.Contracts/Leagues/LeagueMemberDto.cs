using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public record LeagueMemberDto(
    string UserId,
    string FullName, 
    DateTime JoinedAt,
    LeagueMemberStatus Status);
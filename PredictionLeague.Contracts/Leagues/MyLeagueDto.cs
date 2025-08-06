using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public record MyLeagueDto(
    int Id,
    string Name,
    string SeasonName,
    LeagueMemberStatus Status,
    long? Rank,
    int? MemberCount
);
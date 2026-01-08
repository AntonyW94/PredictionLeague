using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Dashboard;

public record LeagueRequestDto(
    int LeagueId,
    string LeagueName,
    string SeasonName,
    LeagueMemberStatus Status,
    DateTimeOffset SentAt,    
    string AdminName,
    int MemberCount,
    decimal EntryFee,
    decimal PotValue
);
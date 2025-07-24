using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public record PublicLeagueDto(
    int Id, 
    string Name, 
    string SeasonName,
    decimal Price,
    DateTime EntryDeadline,
    LeagueMemberStatus? Status);
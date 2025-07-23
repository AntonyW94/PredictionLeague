namespace PredictionLeague.Contracts.Leagues;

public record PublicLeagueDto(
    int Id, 
    string Name, 
    string SeasonName,
    bool IsMember);
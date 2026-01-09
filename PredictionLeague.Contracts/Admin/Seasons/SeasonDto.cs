namespace PredictionLeague.Contracts.Admin.Seasons;

public record SeasonDto(
    int Id,
    string Name,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsActive,
    int NumberOfRounds, 
    int RoundCount
) : SeasonLookupDto(Id, Name, StartDateUtc);
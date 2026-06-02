namespace ThePredictions.Contracts.Admin.Seasons;

public record SeasonLookupDto(
    int Id,
    string Name,
    DateTime StartDateUtc,
    bool IsTournament = false);
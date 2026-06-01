namespace ThePredictions.Contracts.Admin.Seasons;

public record SeasonDto(
    int Id,
    string Name,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsActive,
    int NumberOfRounds,
    int CompetitionId,
    string CompetitionName,
    int CompetitionType,
    int? ApiLeagueId,
    int RoundCount,
    int DraftCount,
    int PublishedCount,
    int InProgressCount,
    int CompletedCount,
    decimal? PassStandardPrice,
    decimal? PassPremiumPrice
) : SeasonLookupDto(Id, Name, StartDateUtc);

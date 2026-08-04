using ThePredictions.Domain.Common.Enumerations;

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
    CompetitionType CompetitionType,
    int? ApiLeagueId,
    int RoundCount,
    int DraftCount,
    int PublishedCount,
    int InProgressCount,
    int CompletedCount,
    int TeamCount,
    decimal? PassStandardPrice,
    decimal? PassPremiumPrice,
    int PassHolderCount
) : SeasonLookupDto(Id, Name, StartDateUtc);

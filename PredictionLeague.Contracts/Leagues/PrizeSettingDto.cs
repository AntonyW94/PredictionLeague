using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public record PrizeSettingDto(
    PrizeType PrizeType,
    int Rank,
    decimal PrizeAmount,
    string? PrizeDescription,
    int? Month,
    int? RoundNumber
);
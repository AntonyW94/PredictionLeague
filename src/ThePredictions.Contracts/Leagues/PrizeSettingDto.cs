using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

public record PrizeSettingDto(
    PrizeType PrizeType,
    int Rank,
    decimal PrizeAmount,
    string? Stage
);
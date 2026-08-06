using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public record PrizeSettingDto(
    PrizeType PrizeType,
    int Rank,
    decimal PrizeAmount,
    string? Stage
);

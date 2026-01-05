using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public record MyLeagueDto(
    int Id,
    string Name,
    string SeasonName,
    long? RoundRank,
    long? MonthRank,
    long? Rank,
    string CurrentRound,
    string CurrentMonth,
    int? MemberCount,
    decimal PrizeMoneyWon,
    decimal PrizeMoneyRemaining,
    decimal TotalPrizeFund
);
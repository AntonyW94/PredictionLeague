namespace PredictionLeague.Contracts.Leagues;

public record MyLeagueDto(
    int Id,
    string Name,
    string SeasonName,

    string CurrentRound,
    string CurrentMonth,
    int? MemberCount,

    int? Rank,
    int? MonthRank,
    int? RoundRank,

    int? PreRoundOverallRank,
    int? PreRoundMonthRank,
    int? StableRoundRank,
    string RoundStatus,
    int InProgressCount,
    int CompletedCount,

    decimal PrizeMoneyWon,
    decimal PrizeMoneyRemaining,
    decimal TotalPrizeFund,
    decimal EntryFee
);
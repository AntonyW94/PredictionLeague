namespace ThePredictions.Contracts.Leagues;

public record MyLeagueDto(
    int Id,
    string Name,
    string SeasonName,
    int CompetitionType,
    DateTime? SeasonStartDateUtc,
    DateTime? EntryDeadlineUtc,

    string CurrentRound,
    string CurrentMonth,
    DateTime? RoundStartDateUtc,
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
    decimal EntryFee,
    bool IsFree,

    int RoundsWon,
    int MonthsWon,

    bool IsFinished,
    bool IsArchivedByUser,

    string? StageName,
    int? StageRank,
    int? PreRoundStageRank
);
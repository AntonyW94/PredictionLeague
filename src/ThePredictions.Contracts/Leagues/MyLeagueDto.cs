using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record MyLeagueDto(
    int Id,
    string Name,
    string SeasonName,
    CompetitionType CompetitionType,
    DateTime? SeasonStartDateUtc,
    DateTime? EntryDeadlineUtc,

    // Null when the league's season has no round worth showing - which the old query already returned as NULL
    // into these non-nullable properties. Declared honestly now rather than relying on that going unnoticed.
    string? CurrentRound,
    string? CurrentMonth,
    DateTime? RoundStartDateUtc,
    int? MemberCount,

    int? Rank,
    int? MonthRank,
    int? RoundRank,

    int? PreRoundOverallRank,
    int? PreRoundMonthRank,
    int? StableRoundRank,
    string? RoundStatus,
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

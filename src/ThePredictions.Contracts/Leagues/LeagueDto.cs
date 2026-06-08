namespace ThePredictions.Contracts.Leagues;

public record LeagueDto(
    int Id,
    string Name,
    string SeasonName,
    int MemberCount,
    decimal Price,
    string EntryCode,
    DateTime EntryDeadlineUtc,
    int PointsForExactScore,
    int PointsForCorrectResult,
    int SeasonId = 0,
    bool IsTournament = false,
    bool HasPrizeScheme = false,
    bool RequiresMemberApproval = true,
    bool IsListed = false
);
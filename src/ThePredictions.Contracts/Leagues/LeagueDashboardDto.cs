using ThePredictions.Contracts.Admin.Rounds;

namespace ThePredictions.Contracts.Leagues;

public class LeagueDashboardDto
{
    public string LeagueName { get; init; } = string.Empty;
    public int CompetitionType { get; init; }
    public DateTime? SeasonStartDateUtc { get; init; }
    public int MemberCount { get; init; }
    public decimal TotalPrizeFund { get; init; }
    public bool IsFinished { get; init; }
    public bool IsFree { get; init; }
    public List<LeagueDashboardMemberDto> Members { get; init; } = [];
    public List<RoundDto> ViewableRounds { get; init; } = [];
}
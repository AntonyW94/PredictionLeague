using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class LeagueDashboardDto
{
    public string LeagueName { get; init; } = string.Empty;
    public CompetitionType CompetitionType { get; init; }
    public DateTime? SeasonStartDateUtc { get; init; }
    public DateTime? EntryDeadlineUtc { get; init; }
    public int MemberCount { get; init; }
    public decimal TotalPrizeFund { get; init; }
    public bool IsFinished { get; init; }
    public bool IsFree { get; init; }
    public List<LeagueDashboardMemberDto> Members { get; init; } = [];
    public List<RoundDto> ViewableRounds { get; init; } = [];
}

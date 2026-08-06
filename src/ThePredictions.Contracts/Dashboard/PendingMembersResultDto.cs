using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Dashboard;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class PendingMembersResultDto
{
    public bool IsAdminOfOpenLeague { get; init; }
    public List<AdminLeagueSummaryDto> AdminLeagues { get; init; } = [];
    public List<PendingLeagueMemberDto> Members { get; init; } = [];
}

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

    /// <summary>
    /// The rounds a member may open, newest first. Drafts are left out: a draft is a round still being prepared, so
    /// it must never be selectable or predictable.
    /// </summary>
    public List<RoundDto> ViewableRounds { get; init; } = [];

    /// <summary>
    /// Every round of the season in playing order, drafts included, for the pre-deadline round-structure preview.
    /// This is a read-only shape of the season ahead, never a list of rounds anyone can open - use
    /// <see cref="ViewableRounds"/> for that.
    /// </summary>
    public List<RoundDto> SeasonRounds { get; init; } = [];
}

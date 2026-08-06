using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class LeagueMembersPageDto
{
    public string LeagueName { get; init; } = string.Empty;
    public List<LeagueMemberDto> Members { get; init; } = [];
}

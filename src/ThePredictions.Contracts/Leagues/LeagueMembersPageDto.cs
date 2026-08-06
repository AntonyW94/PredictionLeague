using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public class LeagueMembersPageDto
{
    public string LeagueName { get; init; } = string.Empty;
    public List<LeagueMemberDto> Members { get; init; } = [];
}

namespace PredictionLeague.Contracts.Leagues;

public class LeagueMembersPageDto
{
    public string LeagueName { get; init; } = string.Empty;
    public List<LeagueMemberDto> Members { get; init; } = new();
}
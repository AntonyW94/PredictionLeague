using ThePredictions.Contracts.Admin.Teams;

namespace ThePredictions.Application.Features.Admin.Teams.Queries;

/// <summary>How teams are ordered and shaped, in one place for the screens that list them.</summary>
internal static class TeamMapping
{
    /// <summary>Alphabetical by name, with an explicit comparer rather than the database's collation.</summary>
    public static IEnumerable<TeamRow> InNameOrder(IEnumerable<TeamRow> teams) =>
        teams.OrderBy(team => team.Name, StringComparer.InvariantCultureIgnoreCase);

    public static TeamDto ToDto(TeamRow team) =>
        new(team.Id, team.Name, team.ShortName, team.LogoUrl, team.Abbreviation, team.ApiTeamId);
}

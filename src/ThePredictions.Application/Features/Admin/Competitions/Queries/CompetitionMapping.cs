using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

/// <summary>How competitions are ordered and shaped, in one place for the two screens that show them.</summary>
internal static class CompetitionMapping
{
    /// <summary>
    /// Alphabetical by name, with an explicit comparer rather than the database's collation - so the list reads the same
    /// whichever database is answering.
    /// </summary>
    public static IEnumerable<CompetitionRow> InNameOrder(IEnumerable<CompetitionRow> competitions) =>
        competitions.OrderBy(competition => competition.Name, StringComparer.InvariantCultureIgnoreCase);

    public static CompetitionDto ToDto(CompetitionRow competition) =>
        new(competition.Id,
            competition.Code,
            competition.Name,
            competition.Type,
            competition.LogoUrl,
            competition.Description,
            competition.ApiLeagueId,
            competition.SeasonCount);
}

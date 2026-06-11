using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

/// <summary>
/// Presentation helpers for the league welcome email: the pot total (recurring prizes multiplied
/// by their occurrence count), the prize breakdown grouped into categories, and one line per
/// enabled boost - Brevo's template language can't compute any of these.
/// </summary>
public static class LeagueWelcomeEmailFormatter
{
    public static string PrizePot(LeagueWelcomeLeague league) =>
        PrizeNotificationFormatter.Money(league.Prizes.Sum(p => p.Amount * Occurrences(p.PrizeType, league)));

    /// <summary>
    /// The prize breakdown grouped the same way as the website's prizes page: Overall, one section
    /// per tournament stage, then the recurring/special prizes. The top prize of each ranked
    /// section is flagged for the trophy treatment.
    /// </summary>
    public static List<LeagueWelcomePrizeSection> PrizeSections(LeagueWelcomeLeague league)
    {
        var sections = new List<LeagueWelcomePrizeSection>();

        var overall = league.Prizes
            .Where(p => p.PrizeType == PrizeType.Overall)
            .OrderBy(p => p.Rank)
            .Select(p => new LeagueWelcomePrizeLine($"{DigestEmailFormatter.Ordinal(p.Rank)} place", PrizeNotificationFormatter.Money(p.Amount), p.Rank == 1))
            .ToList();

        if (overall.Count > 0)
            sections.Add(new LeagueWelcomePrizeSection("Overall", overall));

        foreach (var stage in league.Prizes.Where(p => p.PrizeType == PrizeType.Stages).GroupBy(p => p.Stage).OrderBy(g => g.Key))
        {
            var rows = stage
                .OrderBy(p => p.Rank)
                .Select(p => new LeagueWelcomePrizeLine($"{DigestEmailFormatter.Ordinal(p.Rank)} place", PrizeNotificationFormatter.Money(p.Amount), p.Rank == 1))
                .ToList();

            sections.Add(new LeagueWelcomePrizeSection(string.IsNullOrWhiteSpace(stage.Key) ? "Stage" : stage.Key, rows));
        }

        var other = new List<LeagueWelcomePrizeLine>();

        foreach (var prize in league.Prizes.Where(p => p.PrizeType == PrizeType.Round))
            other.Add(new LeagueWelcomePrizeLine($"Round winner - each of the {league.NumberOfRounds} rounds", PrizeNotificationFormatter.Money(prize.Amount), IsTop: false));

        foreach (var prize in league.Prizes.Where(p => p.PrizeType == PrizeType.Monthly))
            other.Add(new LeagueWelcomePrizeLine($"Monthly winner - each of the {league.NumberOfMonths} months", PrizeNotificationFormatter.Money(prize.Amount), IsTop: false));

        foreach (var prize in league.Prizes.Where(p => p.PrizeType == PrizeType.MostExactScores))
            other.Add(new LeagueWelcomePrizeLine("Most exact scores", PrizeNotificationFormatter.Money(prize.Amount), IsTop: false));

        if (other.Count > 0)
            sections.Add(new LeagueWelcomePrizeSection("Other prizes", other));

        return sections;
    }

    public static List<(string Name, string Description, string Usage, string ImageUrl)> BoostLines(LeagueWelcomeLeague league)
    {
        return league.Boosts
            .Select(boost => (boost.Name, boost.Description ?? string.Empty, Usage(boost, league.NumberOfRounds), boost.ImageUrl ?? string.Empty))
            .ToList();
    }

    private static string Usage(LeagueWelcomeBoost boost, int numberOfRounds)
    {
        var uses = boost.TotalUsesPerSeason == 1
            ? "Can be used once this season"
            : $"Can be used {boost.TotalUsesPerSeason} times this season";

        // A single window spanning the whole season adds nothing over the season cap.
        var meaningfulWindows = boost.Windows
            .Where(w => w.StartRoundNumber > 1 || w.EndRoundNumber < numberOfRounds || boost.Windows.Count > 1)
            .ToList();

        if (meaningfulWindows.Count == 0)
            return uses;

        var windowParts = meaningfulWindows.Select(w =>
        {
            var range = w.StartRoundNumber == w.EndRoundNumber
                ? $"round {w.StartRoundNumber}"
                : $"rounds {w.StartRoundNumber}-{w.EndRoundNumber}";

            return $"{range} (max {w.MaxUsesInWindow})";
        });

        return $"{uses}: {string.Join(", ", windowParts)}";
    }

    private static int Occurrences(PrizeType prizeType, LeagueWelcomeLeague league) => prizeType switch
    {
        PrizeType.Round => league.NumberOfRounds,
        PrizeType.Monthly => league.NumberOfMonths,
        _ => 1
    };
}

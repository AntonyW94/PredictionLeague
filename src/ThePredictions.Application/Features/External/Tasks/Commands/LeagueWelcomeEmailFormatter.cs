using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.External.Tasks.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

/// <summary>
/// Presentation helpers for the league welcome email: the pot total (recurring prizes multiplied
/// by their occurrence count), one human-readable line per prize, and one line per enabled boost -
/// Brevo's template language can't compute any of these.
/// </summary>
public static class LeagueWelcomeEmailFormatter
{
    public static string PrizePot(LeagueWelcomeLeague league) =>
        PrizeNotificationFormatter.Money(league.Prizes.Sum(p => p.Amount * Occurrences(p.PrizeType, league)));

    public static List<(string Title, string Value)> PrizeLines(LeagueWelcomeLeague league)
    {
        var lines = new List<(string Title, string Value)>();

        foreach (var prize in league.Prizes.Where(p => p.PrizeType == PrizeType.Overall).OrderBy(p => p.Rank))
            lines.Add(($"Overall - {DigestEmailFormatter.Ordinal(prize.Rank)}", PrizeNotificationFormatter.Money(prize.Amount)));

        foreach (var prize in league.Prizes.Where(p => p.PrizeType == PrizeType.Stages).OrderBy(p => p.Stage).ThenBy(p => p.Rank))
        {
            var stage = string.IsNullOrWhiteSpace(prize.Stage) ? "Stage" : prize.Stage;
            lines.Add(($"{stage} - {DigestEmailFormatter.Ordinal(prize.Rank)}", PrizeNotificationFormatter.Money(prize.Amount)));
        }

        foreach (var prize in league.Prizes.Where(p => p.PrizeType == PrizeType.Round))
            lines.Add(($"Round winner - each of the {league.NumberOfRounds} rounds", PrizeNotificationFormatter.Money(prize.Amount)));

        foreach (var prize in league.Prizes.Where(p => p.PrizeType == PrizeType.Monthly))
            lines.Add(($"Monthly winner - each of the {league.NumberOfMonths} months", PrizeNotificationFormatter.Money(prize.Amount)));

        foreach (var prize in league.Prizes.Where(p => p.PrizeType == PrizeType.MostExactScores))
            lines.Add(("Most exact scores", PrizeNotificationFormatter.Money(prize.Amount)));

        return lines;
    }

    public static List<(string Name, string Description, string Usage)> BoostLines(LeagueWelcomeLeague league)
    {
        return league.Boosts
            .Select(boost => (boost.Name, boost.Description ?? string.Empty, Usage(boost, league.NumberOfRounds)))
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

using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class LeagueRecordsDto
{
    public bool IsFree { get; init; }

    public string? TopRoundPlayerName { get; init; }
    public int TopRoundPoints { get; init; }
    public int? TopRoundNumber { get; init; }

    public string? LowestRoundPlayerName { get; init; }
    public int LowestRoundPoints { get; init; }
    public int? LowestRoundNumber { get; init; }

    public string? MostExactInRoundPlayerName { get; init; }
    public int MostExactInRoundCount { get; init; }
    public int? MostExactInRoundNumber { get; init; }

    public string? ChampionName { get; init; }
    public int ChampionPoints { get; init; }

    public string? TopEarnerName { get; init; }
    public decimal TopEarnerAmount { get; init; }

    public string? MostRoundsWonPlayerName { get; init; }
    public int MostRoundsWonCount { get; init; }

    public string? MostMonthsWonPlayerName { get; init; }
    public int MostMonthsWonCount { get; init; }

    public int TotalExactScores { get; init; }

    public string? BiggestPrizePlayerName { get; init; }
    public decimal BiggestPrizeAmount { get; init; }
    public string? BiggestPrizeDescription { get; init; }

    public int? HighestGameweekRoundNumber { get; init; }
    public int HighestGameweekPoints { get; init; }
}

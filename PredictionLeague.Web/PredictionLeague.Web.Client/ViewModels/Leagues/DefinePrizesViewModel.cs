using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Web.Client.Utilities;

namespace PredictionLeague.Web.Client.ViewModels.Leagues;

public class DefinePrizesViewModel
{
    public decimal PrizePot { get; }
    public int NumberOfRounds { get; }
    public int NumberOfMonths { get; }

    public List<DefinePrizeSettingDto> PrizeSettings { get; set; } = new();

    public decimal MonthlyWinnerAmount { get; set; }
    public decimal RoundWinnerAmount { get; set; }

    public decimal TotalAllocated => PrizeSettings.Sum(p => p.PrizeAmount) + MonthlyWinnerAmount * NumberOfMonths + RoundWinnerAmount * NumberOfRounds;

    public decimal RemainingToAllocate => PrizePot - TotalAllocated;

    public DefinePrizesViewModel(decimal prizePot, int numberOfRounds, DateTime seasonStartDate, DateTime seasonEndDate)
    {
        PrizePot = prizePot;
        NumberOfRounds = numberOfRounds;

        var months = new HashSet<string>();
        for (var date = seasonStartDate; date <= seasonEndDate; date = date.AddMonths(1))
        {
            months.Add(date.ToString("MMMM"));
        }
        NumberOfMonths = months.Count;

        PrizeSettings.Add(new DefinePrizeSettingDto { PrizeType = PrizeType.Overall, Rank = 1, PrizeDescription = "1st Place" });
        PrizeSettings.Add(new DefinePrizeSettingDto { PrizeType = PrizeType.MostExactScores, Rank = 1, PrizeDescription = "Most Correct Scores" });
    }

    public void AddOverallPrize()
    {
        var overallPrizes = PrizeSettings.Where(p => p.PrizeType == PrizeType.Overall).ToList();
        var nextRank = overallPrizes.Any() ? overallPrizes.Max(p => p.Rank) + 1 : 1;
        var description = $"{nextRank}{FormattingUtilities.GetOrdinal(nextRank)} Place";

        PrizeSettings.Add(new DefinePrizeSettingDto { PrizeType = PrizeType.Overall, Rank = nextRank, PrizeDescription = description });
    }

    public void RemoveOverallPrize(DefinePrizeSettingDto prizeToRemove)
    {
        PrizeSettings.Remove(prizeToRemove);
    }

    public List<DefinePrizeSettingDto> ToFinalPrizeSettings()
    {
        var finalSettings = new List<DefinePrizeSettingDto>(PrizeSettings);

        if (MonthlyWinnerAmount > 0)
            finalSettings.Add(new DefinePrizeSettingDto { PrizeType = PrizeType.Monthly, Rank = 1, PrizeAmount = MonthlyWinnerAmount });
        
        if (RoundWinnerAmount > 0)
            finalSettings.Add(new DefinePrizeSettingDto { PrizeType = PrizeType.Round, Rank = 1, PrizeAmount = RoundWinnerAmount });

        return finalSettings;
    }
}
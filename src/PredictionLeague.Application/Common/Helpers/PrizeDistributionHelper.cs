namespace PredictionLeague.Application.Common.Helpers;

public static class PrizeDistributionHelper
{
    public static List<decimal> DistributePrizeMoney(decimal totalAmount, int winnerCount)
    {
        if (winnerCount == 0)
            return [];

        var totalPennies = (int)(totalAmount * 100);
        var basePennies = totalPennies / winnerCount;
        var remainderPennies = totalPennies % winnerCount;

        var amountsInPennies = Enumerable.Repeat(basePennies, winnerCount).ToList();

        var random = new Random();
        for (var i = 0; i < remainderPennies; i++)
        {
            int winnerIndex;
            do
            {
                winnerIndex = random.Next(0, winnerCount);
            }
            while (amountsInPennies[winnerIndex] > basePennies);

            amountsInPennies[winnerIndex]++;
        }

        return amountsInPennies.Select(pennies => (decimal)pennies / 100).ToList();
    }
}
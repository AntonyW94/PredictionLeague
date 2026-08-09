namespace ThePredictions.Domain.Services.Prizes;

/// <summary>
/// Splits one settled prize between players who tied for it, to the penny.
/// </summary>
/// <remarks>
/// A prize rarely divides exactly: £10 between three winners leaves one spare penny. Rather than
/// always favouring the same seat, the remainder pennies are handed out at random, at most one each,
/// so a tie is settled fairly rather than by list order. The consequence is that the same tie settled
/// twice can allocate the spare penny differently - deliberate, and the reason a test asserts the
/// totals and bounds rather than the exact per-winner figures.
///
/// Distinct from <see cref="PrizeApportionmentService"/>, which builds the up-front breakdown of a
/// whole scheme. This divides a single amount that has already been won.
/// </remarks>
public static class SharedPrizeSplitter
{
    /// <summary>
    /// Divides <paramref name="totalAmount"/> between <paramref name="winnerCount"/> winners. Returns
    /// an empty list when there are no winners; otherwise the returned amounts always sum to
    /// <paramref name="totalAmount"/> and differ from one another by at most a penny.
    /// </summary>
    public static List<decimal> Split(decimal totalAmount, int winnerCount)
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
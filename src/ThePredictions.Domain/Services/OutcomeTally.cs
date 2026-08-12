using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services;

/// <summary>
/// Counting how a set of predictions turned out: exact scores, correct results, and the rest.
/// </summary>
/// <remarks>
/// This existed twice. The stored tally every round writes was a <c>MERGE</c> with three
/// <c>SUM(CASE WHEN up.[Outcome] = @X THEN 1 ELSE 0 END)</c> columns and an <c>up.[Outcome] &lt;&gt; 0</c> filter, and the
/// active-rounds tile counted the same three things in C# for the one player looking at it. Neither had a test, and both
/// are the same rule: how a prediction turned out is <see cref="UserPrediction.SetOutcome"/>'s job, and this is only the
/// counting.
///
/// A prediction still waiting on its result counts towards none of the three - which is what the SQL's
/// <c>&lt;&gt; 0</c> said, <c>0</c> being <see cref="PredictionOutcome.Pending"/>. Saying it here means the next reader
/// does not have to know that the enum's first member is the unjudged one.
/// </remarks>
public static class OutcomeTally
{
    /// <summary>For predictions that have an outcome each - the stored tally's case.</summary>
    public static OutcomeCounts For(IEnumerable<PredictionOutcome> outcomes) =>
        For(outcomes.Select(outcome => (PredictionOutcome?)outcome));

    /// <summary>
    /// For fixtures a player may not have predicted at all, where the absence and an unjudged prediction mean the same
    /// thing to the count - the active-rounds tile's case.
    /// </summary>
    public static OutcomeCounts For(IEnumerable<PredictionOutcome?> outcomes)
    {
        var exactScores = 0;
        var correctResults = 0;
        var incorrect = 0;

        foreach (var outcome in outcomes)
        {
            switch (outcome)
            {
                case PredictionOutcome.ExactScore:
                    exactScores++;
                    break;
                case PredictionOutcome.CorrectResult:
                    correctResults++;
                    break;
                case PredictionOutcome.Incorrect:
                    incorrect++;
                    break;
            }
        }

        return new OutcomeCounts(exactScores, correctResults, incorrect);
    }
}

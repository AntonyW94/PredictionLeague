using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Turns a prize scheme (saved or draft) into a live, round-number breakdown DTO at a given entrant
/// count. Pure compute - safe to use from query handlers; delegates all arithmetic to the domain
/// apportionment service so the 100%-covered maths stays in one place.
/// </summary>
public interface IPrizeEvaluator
{
    PrizeBreakdownDto Evaluate(PrizeSchemeEvaluationRequest request);
}

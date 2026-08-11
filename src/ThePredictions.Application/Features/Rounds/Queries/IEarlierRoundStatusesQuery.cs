using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// The statuses of every round in the season that comes before a given round number.
///
/// Returns statuses rather than answering "are they all finished". That question is a rule - the reminder
/// job holds back its early milestones while an earlier round is still unscored, because players want the
/// previous round's results before predicting the next - and it used to be a <c>COUNT(1) ... WHERE Status
/// &lt;&gt; @Completed</c> compared against zero, which is the rule written in SQL.
/// </summary>
public interface IEarlierRoundStatusesQuery
{
    Task<IReadOnlyList<RoundStatus>> ExecuteAsync(int seasonId, int roundNumber, CancellationToken cancellationToken);
}

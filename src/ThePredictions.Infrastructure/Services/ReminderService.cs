using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Services;

/// <summary>
/// Decides when a prediction reminder is due and who should get one.
///
/// No longer carries SQL, and therefore no longer carries <c>[ExcludeFromCodeCoverage]</c>. Its two reads
/// went to the persistence adapter, and with them went three rules this class had written out in T-SQL
/// despite the domain already owning all three:
///
/// <list type="bullet">
/// <item>which fixtures a player can still act on - now <c>Match.IsOpenForPrediction</c>, the rule the SQL
/// predicate said in its own comment that it mirrored;</item>
/// <item>the next deadline to count down to - now <c>Round.GetNextPredictionDeadline</c> for both the
/// milestone schedule and the email, where the SQL used to recompute it beside a C# call doing the same
/// thing;</item>
/// <item>the round's display name - now <c>Round.GetDisplayNameOrDefault</c>.</item>
/// </list>
/// </summary>
public class ReminderService(
    IRoundCompletionQuery completionQuery,
    IEarlierRoundStatusesQuery earlierRoundStatusesQuery) : IReminderService
{
    public async Task<bool> ShouldSendReminderAsync(Round round, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Milestones are measured against the next batch to lock, not the round deadline. In a combined
        // round the semi-finals lock at the round deadline while the final and third-place playoff lock
        // later, so keying off the next lock gives that later batch its own reminder wave. For a normal
        // round every match locks at the round deadline, so this is simply the round deadline.
        var nextLock = round.GetNextPredictionDeadline(nowUtc);
        if (nextLock == null)
            return false;

        var deadline = nextLock.Value;
        var lastSent = round.LastReminderSentUtc;

        // The two early milestones (5 and 3 days out) are held back while any earlier round in the
        // season is still unfinished. Tournament rounds sit close together, so a 5-day reminder can
        // land before the current round has even finished, and players want that round's results
        // before predicting the next. The 1-day, 6-hour and 1-hour milestones always send - the
        // deadline is imminent regardless of how late the previous round finished. The check is
        // re-evaluated every run, so once the previous round completes the next passed milestone fires.
        var earlyMilestonesAllowed = await PreviousRoundsCompletedAsync(round, cancellationToken);

        var milestones = new List<DateTime>
        {
            deadline.AddDays(-1),
            deadline.AddHours(-6),
            deadline.AddHours(-1)
        };

        if (earlyMilestonesAllowed)
        {
            milestones.Add(deadline.AddDays(-5));
            milestones.Add(deadline.AddDays(-3));
        }

        foreach (var targetTime in milestones.OrderByDescending(m => m))
        {
            if (nowUtc < targetTime)
                continue;

            if (lastSent == null)
                return true;

            return lastSent.Value < targetTime;
        }

        return false;
    }

    private async Task<bool> PreviousRoundsCompletedAsync(Round round, CancellationToken cancellationToken)
    {
        var earlierStatuses = await earlierRoundStatusesQuery.ExecuteAsync(
            round.SeasonId, round.RoundNumber, cancellationToken);

        return earlierStatuses.All(status => status == RoundStatus.Completed);
    }

    public async Task<List<ChaseUserDto>> GetUsersMissingPredictionsAsync(
        int roundId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Every approved member across every league in the round's season - the same facts the admin
        // round-completion view reads, which is why they share one port. Passing no league id is what makes
        // it season-wide.
        var data = await completionQuery.ExecuteAsync(roundId, leagueId: null, cancellationToken);
        if (data == null)
            return [];

        // Chase a member who is still missing a prediction for at least one fixture they can act on, so
        // nobody is nagged about a fixture they can no longer change. Tournament rounds reveal their
        // fixtures over time - a knockout round is published once its first tie has confirmed teams - so a
        // member who predicted the only confirmed match today should be chased again once more ties are
        // confirmed. That falls out of asking the question fresh each run.
        var openFixtures = data.Round.Matches
            .Where(match => match.IsOpenForPrediction(nowUtc, data.Round.DeadlineUtc))
            .ToList();

        if (openFixtures.Count == 0)
            return [];

        var openFixtureIds = openFixtures.Select(match => match.Id).ToHashSet();

        var predictedByUser = data.Predictions
            .Where(prediction => openFixtureIds.Contains(prediction.MatchId))
            .GroupBy(prediction => prediction.UserId)
            .ToDictionary(group => group.Key, group => group.Count());

        var roundName = data.Round.GetDisplayNameOrDefault();

        // The deadline shown in the email is the earliest lock among the fixtures being chased, which is
        // exactly what GetNextPredictionDeadline now answers - and the same value the milestone schedule
        // above measures against, so the email and the send decision cannot disagree. Non-null because at
        // least one fixture is open, which is the same condition the method tests.
        var deadlineUtc = data.Round.GetNextPredictionDeadline(nowUtc)!.Value;

        return data.Participants
            .Where(participant => Predicted(predictedByUser, participant.UserId) < openFixtureIds.Count)
            .Select(participant => new ChaseUserDto(
                participant.Email, participant.FirstName, roundName, deadlineUtc, participant.UserId))
            .ToList();
    }

    private static int Predicted(IReadOnlyDictionary<string, int> predictedByUser, string userId) =>
        predictedByUser.TryGetValue(userId, out var count) ? count : 0;
}

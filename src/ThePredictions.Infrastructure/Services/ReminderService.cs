using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Services;

public class ReminderService(IApplicationReadDbConnection dbConnection) : IReminderService
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
            if (nowUtc >= targetTime)
                return lastSent == null || lastSent < targetTime;
        }

        return false;
    }

    private async Task<bool> PreviousRoundsCompletedAsync(Round round, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM [Rounds] r
            WHERE r.[SeasonId] = @SeasonId
                AND r.[RoundNumber] < @RoundNumber
                AND r.[Status] <> @CompletedStatus;";

        var incompletePreviousRounds = await dbConnection.QuerySingleOrDefaultAsync<int>(
            sql,
            cancellationToken,
            new
            {
                round.SeasonId,
                round.RoundNumber,
                CompletedStatus = nameof(RoundStatus.Completed)
            });

        return incompletePreviousRounds == 0;
    }

    public async Task<List<ChaseUserDto>> GetUsersMissingPredictionsAsync(int roundId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Chase any approved member who is still missing a prediction for at least one match they
        // can act on - a fixture with confirmed teams that is not postponed and has not yet locked.
        // Tournament rounds reveal their fixtures over time (a knockout round is published once its
        // first tie has confirmed teams), so a member who predicts the only confirmed match today
        // should be reminded again at the next milestone once further ties are confirmed. We only
        // count matches still open for prediction, so a member is never nagged about a fixture they
        // can no longer change.
        const string sql = @"
            SELECT DISTINCT
                u.[Email],
                u.[FirstName],
                CASE
                    WHEN LEN(LTRIM(RTRIM(r.[DisplayName]))) > 0 THEN r.[DisplayName]
                    ELSE 'Round ' + CONVERT(NVARCHAR(MAX), r.[RoundNumber])
                END AS RoundName,
                CASE
                    WHEN r.[DeadlineUtc] > @NowUtc THEN r.[DeadlineUtc]
                    ELSE COALESCE(
                        (
                            SELECT MIN(nm.[CustomLockTimeUtc])
                            FROM [Matches] nm
                            WHERE nm.[RoundId] = r.[Id]
                                AND nm.[HomeTeamId] IS NOT NULL
                                AND nm.[AwayTeamId] IS NOT NULL
                                AND nm.[Status] <> @PostponedStatus
                                AND nm.[CustomLockTimeUtc] > @NowUtc
                        ),
                        r.[DeadlineUtc])
                END AS DeadlineUtc,
                u.[Id] AS UserId
            FROM
                [AspNetUsers] u
            JOIN
                [LeagueMembers] lm ON u.[Id] = lm.[UserId]
            JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN
                [Rounds] r ON l.[SeasonId] = r.[SeasonId]
            WHERE
                r.[Id] = @RoundId
                AND lm.[Status] = @ApprovedStatus
                AND EXISTS (
                    SELECT 1
                    FROM [Matches] m
                    WHERE m.[RoundId] = r.[Id]
                        AND m.[HomeTeamId] IS NOT NULL
                        AND m.[AwayTeamId] IS NOT NULL
                        AND m.[Status] <> @PostponedStatus
                        AND (m.[CustomLockTimeUtc] IS NULL OR m.[CustomLockTimeUtc] > @NowUtc)
                        AND NOT EXISTS (
                            SELECT 1 FROM [UserPredictions] up
                            WHERE up.[MatchId] = m.[Id] AND up.[UserId] = u.[Id]
                        )
              );";

        return (await dbConnection.QueryAsync<ChaseUserDto>(
            sql,
            cancellationToken,
            new
            {
                RoundId = roundId,
                NowUtc = nowUtc,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                PostponedStatus = nameof(MatchStatus.Postponed)
            })).ToList();
    }
}
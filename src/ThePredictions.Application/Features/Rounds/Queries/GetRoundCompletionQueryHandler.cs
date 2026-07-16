using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Rounds;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Rounds.Queries;

public class GetRoundCompletionQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetRoundCompletionQuery, RoundCompletionDto?>
{
    // A fixture counts towards completion only while a player can still act on it: teams confirmed,
    // not postponed, and not yet locked. Kept in lockstep with the identical predicate in
    // ReminderService.GetUsersMissingPredictionsAsync - change both together.
    private const string PredictableMatchPredicate = @"
        m.[RoundId] = @RoundId
        AND m.[HomeTeamId] IS NOT NULL
        AND m.[AwayTeamId] IS NOT NULL
        AND m.[Status] <> @PostponedStatus
        AND (m.[CustomLockTimeUtc] IS NULL OR m.[CustomLockTimeUtc] > @NowUtc)";

    public async Task<RoundCompletionDto?> Handle(GetRoundCompletionQuery request, CancellationToken cancellationToken)
    {
        var canSendReminders = await AuthoriseAsync(request, cancellationToken);

        var nowUtc = dateTimeProvider.UtcNow;

        var roundInfo = await dbConnection.QuerySingleOrDefaultAsync<RoundInfoRow>(
            @"
            SELECT
                CASE
                    WHEN LEN(LTRIM(RTRIM(r.[DisplayName]))) > 0 THEN r.[DisplayName]
                    ELSE 'Round ' + CONVERT(NVARCHAR(MAX), r.[RoundNumber])
                END AS RoundName,
                r.[DeadlineUtc]
            FROM
                [Rounds] r
            WHERE
                r.[Id] = @RoundId;",
            cancellationToken,
            new { request.RoundId });

        if (roundInfo == null)
            return null;

        var parameters = new
        {
            request.RoundId,
            request.LeagueId,
            NowUtc = nowUtc,
            ApprovedStatus = nameof(LeagueMemberStatus.Approved),
            PostponedStatus = nameof(MatchStatus.Postponed)
        };

        var predictableMatchCount = await dbConnection.QuerySingleOrDefaultAsync<int>(
            $@"
            SELECT COUNT(*)
            FROM [Matches] m
            WHERE {PredictableMatchPredicate};",
            cancellationToken,
            parameters);

        var participants = (await dbConnection.QueryAsync<ParticipantRow>(
            $@"
            SELECT
                u.[Id] AS UserId,
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                u.[Email] AS Email,
                (
                    SELECT COUNT(*)
                    FROM [Matches] m
                    JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = u.[Id]
                    WHERE {PredictableMatchPredicate}
                ) AS PredictedCount,
                rn.[LastRemindedUtc]
            FROM
                [AspNetUsers] u
            JOIN
                [LeagueMembers] lm ON u.[Id] = lm.[UserId] AND lm.[Status] = @ApprovedStatus
            JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN
                [Rounds] r ON l.[SeasonId] = r.[SeasonId]
            LEFT JOIN
                [PredictionReminderNotifications] rn ON rn.[RoundId] = r.[Id] AND rn.[UserId] = u.[Id]
            WHERE
                r.[Id] = @RoundId
                AND (@LeagueId IS NULL OR l.[Id] = @LeagueId)
            GROUP BY
                u.[Id],
                u.[FirstName],
                u.[LastName],
                u.[Email],
                rn.[LastRemindedUtc];",
            cancellationToken,
            parameters)).ToList();

        var missingFixtures = (await dbConnection.QueryAsync<MissingFixtureRow>(
            $@"
            SELECT DISTINCT
                u.[Id] AS UserId,
                m.[Id] AS MatchId,
                m.[MatchNumber],
                ht.[Name] AS HomeTeam,
                at.[Name] AS AwayTeam
            FROM
                [AspNetUsers] u
            JOIN
                [LeagueMembers] lm ON u.[Id] = lm.[UserId] AND lm.[Status] = @ApprovedStatus
            JOIN
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN
                [Rounds] r ON l.[SeasonId] = r.[SeasonId]
            JOIN
                [Matches] m ON {PredictableMatchPredicate}
            JOIN
                [Teams] ht ON ht.[Id] = m.[HomeTeamId]
            JOIN
                [Teams] at ON at.[Id] = m.[AwayTeamId]
            WHERE
                r.[Id] = @RoundId
                AND (@LeagueId IS NULL OR l.[Id] = @LeagueId)
                AND NOT EXISTS (
                    SELECT 1 FROM [UserPredictions] up
                    WHERE up.[MatchId] = m.[Id] AND up.[UserId] = u.[Id]
                );",
            cancellationToken,
            parameters)).ToList();

        var fixturesByUser = missingFixtures
            .GroupBy(f => f.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MissingFixtureDto>)g
                    .OrderBy(f => f.MatchNumber)
                    .Select(f => new MissingFixtureDto(f.MatchId, f.MatchNumber, f.HomeTeam, f.AwayTeam))
                    .ToList());

        var players = participants
            .Select(p => new RoundCompletionPlayerDto(
                p.UserId,
                p.PlayerName,
                p.Email,
                p.PredictedCount,
                p.LastRemindedUtc,
                fixturesByUser.TryGetValue(p.UserId, out var fixtures) ? fixtures : []))
            .OrderByDescending(p => p.IsPartial)
            .ThenByDescending(p => p.HasEnteredNothing)
            .ThenBy(p => p.PlayerName)
            .ToList();

        return new RoundCompletionDto(
            request.RoundId,
            roundInfo.RoundName,
            roundInfo.DeadlineUtc,
            // "Passed" for chase purposes means nothing is left to predict. PredictableMatchCount already
            // excludes matches that have locked (per-match CustomLockTimeUtc or the round deadline), so a
            // combined round still counts as open while its later batch is unlocked, even though the round
            // deadline that locked the earlier batch has passed.
            DeadlinePassed: predictableMatchCount == 0,
            canSendReminders,
            predictableMatchCount,
            players);
    }

    private async Task<bool> AuthoriseAsync(GetRoundCompletionQuery request, CancellationToken cancellationToken)
    {
        // Global view is admin-only; the league view is readable by any approved member, but only an
        // admin or the league owner may then send reminders.
        if (request.LeagueId == null)
        {
            if (!request.IsSiteAdmin)
                throw new UnauthorizedAccessException("Only an administrator can view round completion across all leagues.");

            return true;
        }

        await membershipService.EnsureApprovedMemberAsync(request.LeagueId.Value, request.CurrentUserId, cancellationToken);

        return request.IsSiteAdmin
               || await membershipService.IsLeagueAdministratorAsync(request.LeagueId.Value, request.CurrentUserId, cancellationToken);
    }

    private record RoundInfoRow(string RoundName, DateTime DeadlineUtc);

    private record ParticipantRow(string UserId, string PlayerName, string Email, int PredictedCount, DateTime? LastRemindedUtc);

    private record MissingFixtureRow(string UserId, int MatchId, int? MatchNumber, string HomeTeam, string AwayTeam);
}

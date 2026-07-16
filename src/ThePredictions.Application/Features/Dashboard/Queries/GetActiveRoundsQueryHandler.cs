using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

public class GetActiveRoundsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetActiveRoundsQuery, IEnumerable<ActiveRoundDto>>
{
    public async Task<IEnumerable<ActiveRoundDto>> Handle(GetActiveRoundsQuery request, CancellationToken cancellationToken)
    {
        // Query 1: Get active rounds (upcoming + in-progress)
        const string roundsSql = @"
            SELECT
                r.[Id],
                s.[Name] AS SeasonName,
                r.[RoundNumber],
                r.[DeadlineUtc],
                r.[Status],
                CAST(CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM [UserPredictions] up
                        JOIN [Matches] m ON up.MatchId = m.Id
                        WHERE m.RoundId = r.Id AND up.UserId = @UserId
                    ) THEN 1
                    ELSE 0
                END AS bit) AS HasUserPredicted,
                r.[DisplayName] AS RoundDisplayName,
                c.[Type] AS CompetitionType,
                COALESCE(
                    (
                        SELECT
                            MAX(COALESCE(lm.[CustomLockTimeUtc], r.[DeadlineUtc]))
                        FROM
                            [Matches] lm
                        WHERE
                            lm.[RoundId] = r.[Id]
                            AND lm.[Status] <> @PostponedStatus
                    ),
                    r.[DeadlineUtc]) AS LatestPredictionDeadlineUtc
            FROM
                [Rounds] r
            JOIN
                [Seasons] s ON r.[SeasonId] = s.[Id]
            JOIN
                [Competitions] c ON s.[CompetitionId] = c.[Id]
            WHERE
                r.[Status] NOT IN (@DraftStatus, @CompletedStatus)
                AND s.[IsActive] = 1
                AND EXISTS (
                    SELECT 1
                    FROM [Matches] m
                    WHERE m.[RoundId] = r.[Id]
                        AND m.[HomeTeamId] IS NOT NULL
                        AND m.[AwayTeamId] IS NOT NULL
                )
                AND r.[SeasonId] IN (
                    SELECT l.[SeasonId]
                    FROM [Leagues] l
                    JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
                    WHERE lm.[UserId] = @UserId AND lm.[Status] = @ApprovedStatus
                )
            ORDER BY
                CASE WHEN r.[Status] = @InProgressStatus THEN 0 ELSE 1 END,
                r.[DeadlineUtc] ASC";

        var parameters = new
        {
            request.UserId,
            DraftStatus = nameof(RoundStatus.Draft),
            CompletedStatus = nameof(RoundStatus.Completed),
            InProgressStatus = nameof(RoundStatus.InProgress),
            ApprovedStatus = nameof(LeagueMemberStatus.Approved),
            PostponedStatus = nameof(MatchStatus.Postponed)
        };

        var rounds = (await dbConnection.QueryAsync<ActiveRoundQueryResult>(
            roundsSql,
            cancellationToken,
            parameters))
            .Where(r => r.DeadlineUtc > DateTime.UtcNow || r.HasUserPredicted)
            .ToList();

        if (!rounds.Any())
            return Enumerable.Empty<ActiveRoundDto>();

        // Query 2: Get matches with predictions and outcomes for all active rounds
        var roundIds = rounds.Select(r => r.Id).ToArray();

        const string matchesSql = @"
            SELECT
                m.[RoundId],
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                up.[Outcome],
                m.[Status],
                m.[ActualHomeTeamScore] AS ActualHomeScore,
                m.[ActualAwayTeamScore] AS ActualAwayScore,
                m.[MatchDateTimeUtc],
                m.[MatchNumber],
                CAST(CASE
                    WHEN m.[HomeTeamId] IS NOT NULL AND m.[AwayTeamId] IS NOT NULL THEN 1
                    ELSE 0
                END AS bit) AS AreTeamsConfirmed,
                m.[PlaceholderHomeName],
                m.[PlaceholderAwayName],
                (
                    SELECT COUNT(*)
                    FROM [UserPredictions] hp
                    WHERE hp.[MatchId] = m.[Id]
                        AND hp.[PredictedHomeScore] > hp.[PredictedAwayScore]
                ) AS HomeCount,
                (
                    SELECT COUNT(*)
                    FROM [UserPredictions] dp
                    WHERE dp.[MatchId] = m.[Id]
                        AND dp.[PredictedHomeScore] = dp.[PredictedAwayScore]
                ) AS DrawCount,
                (
                    SELECT COUNT(*)
                    FROM [UserPredictions] ap
                    WHERE ap.[MatchId] = m.[Id]
                        AND ap.[PredictedHomeScore] < ap.[PredictedAwayScore]
                ) AS AwayCount,
                m.[CustomLockTimeUtc]
            FROM [Matches] m
            LEFT JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = @UserId
            WHERE m.[RoundId] IN @RoundIds
                AND m.[Status] <> @PostponedStatus
            ORDER BY m.[RoundId], m.[MatchDateTimeUtc] ASC, ht.[ShortName] ASC";

        var matches = await dbConnection.QueryAsync<ActiveRoundMatchQueryResult>(
            matchesSql,
            cancellationToken,
            new { request.UserId, RoundIds = roundIds, PostponedStatus = nameof(MatchStatus.Postponed) });

        // Group matches by RoundId for efficient lookup
        var matchesByRound = matches
            .GroupBy(m => m.RoundId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Map to DTOs
        return rounds.Select(r =>
        {
            var status = Enum.Parse<RoundStatus>(r.Status);
            var utcNow = DateTime.UtcNow;

            var activeRoundMatchDtos = matchesByRound.TryGetValue(r.Id, out var roundMatches)
                ? roundMatches.Select(m =>
                {
                    // The prediction split is only revealed once this match itself has locked; before then we
                    // zero the counts so the aggregate never leaks predictions that are still open. In a
                    // combined round the earlier matches reveal at the round deadline while the later ones
                    // stay hidden until their own custom lock time.
                    var revealSplit = (m.CustomLockTimeUtc ?? r.DeadlineUtc) <= utcNow;

                    return new ActiveRoundMatchDto(m.HomeTeamLogoUrl,
                        m.AwayTeamLogoUrl,
                        m.PredictedHomeScore,
                        m.PredictedAwayScore,
                        m.Outcome,
                        Enum.Parse<MatchStatus>(m.Status),
                        m.ActualHomeScore,
                        m.ActualAwayScore,
                        m.MatchDateTimeUtc,
                        m.MatchNumber,
                        m.AreTeamsConfirmed,
                        m.PlaceholderHomeName,
                        m.PlaceholderAwayName,
                        revealSplit,
                        revealSplit ? m.HomeCount : 0,
                        revealSplit ? m.DrawCount : 0,
                        revealSplit ? m.AwayCount : 0);
                })
                : Enumerable.Empty<ActiveRoundMatchDto>();

            // Calculate outcome summary for rounds past their deadline
            OutcomeSummaryDto? outcomeSummary = null;
            if (r.DeadlineUtc <= utcNow && r.HasUserPredicted && roundMatches != null)
            {
                outcomeSummary = new OutcomeSummaryDto(
                    ExactScoreCount: roundMatches.Count(m => m.Outcome == PredictionOutcome.ExactScore),
                    CorrectResultCount: roundMatches.Count(m => m.Outcome == PredictionOutcome.CorrectResult),
                    IncorrectCount: roundMatches.Count(m => m.Outcome == PredictionOutcome.Incorrect));
            }

            return new ActiveRoundDto(
                r.Id,
                r.SeasonName,
                r.RoundNumber,
                r.RoundDisplayName,
                r.CompetitionType == (int)CompetitionType.Tournament,
                r.DeadlineUtc,
                r.LatestPredictionDeadlineUtc,
                r.HasUserPredicted,
                status,
                activeRoundMatchDtos,
                outcomeSummary);
        });
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record ActiveRoundQueryResult(
        int Id,
        string SeasonName,
        int RoundNumber,
        DateTime DeadlineUtc,
        string Status,
        bool HasUserPredicted,
        string? RoundDisplayName,
        int CompetitionType,
        DateTime LatestPredictionDeadlineUtc);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record ActiveRoundMatchQueryResult(
        int RoundId,
        string? HomeTeamLogoUrl,
        string? AwayTeamLogoUrl,
        int? PredictedHomeScore,
        int? PredictedAwayScore,
        PredictionOutcome? Outcome,
        string Status,
        int? ActualHomeScore,
        int? ActualAwayScore,
        DateTime MatchDateTimeUtc,
        int? MatchNumber,
        bool AreTeamsConfirmed,
        string? PlaceholderHomeName,
        string? PlaceholderAwayName,
        int HomeCount,
        int DrawCount,
        int AwayCount,
        DateTime? CustomLockTimeUtc);
}

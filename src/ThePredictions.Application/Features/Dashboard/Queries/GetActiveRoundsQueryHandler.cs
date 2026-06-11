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
                END AS bit) AS HasUserPredicted
            FROM
                [Rounds] r
            JOIN
                [Seasons] s ON r.[SeasonId] = s.[Id]
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
            ApprovedStatus = nameof(LeagueMemberStatus.Approved)
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
                m.[ActualAwayTeamScore] AS ActualAwayScore
            FROM [Matches] m
            INNER JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            INNER JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
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
            var activeRoundMatchDtos = matchesByRound.TryGetValue(r.Id, out var roundMatches)
                ? roundMatches.Select(m => new ActiveRoundMatchDto(m.HomeTeamLogoUrl,
                    m.AwayTeamLogoUrl,
                    m.PredictedHomeScore,
                    m.PredictedAwayScore,
                    m.Outcome,
                    Enum.Parse<MatchStatus>(m.Status),
                    m.ActualHomeScore,
                    m.ActualAwayScore))
                : Enumerable.Empty<ActiveRoundMatchDto>();

            // Calculate outcome summary for rounds past their deadline
            OutcomeSummaryDto? outcomeSummary = null;
            if (r.DeadlineUtc <= DateTime.UtcNow && r.HasUserPredicted && roundMatches != null)
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
                r.DeadlineUtc,
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
        bool HasUserPredicted);

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
        int? ActualAwayScore);
}

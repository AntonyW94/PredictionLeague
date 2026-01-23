using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetActiveRoundsQueryHandler : IRequestHandler<GetActiveRoundsQuery, IEnumerable<ActiveRoundDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetActiveRoundsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

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

        var rounds = (await _dbConnection.QueryAsync<ActiveRoundQueryResult>(
            roundsSql,
            cancellationToken,
            parameters)).ToList();

        if (!rounds.Any())
            return Enumerable.Empty<ActiveRoundDto>();

        // Query 2: Get matches with predictions and outcomes for all active rounds
        var roundIds = rounds.Select(r => r.Id).ToArray();

        const string matchesSql = @"
            SELECT
                m.[RoundId],
                m.[Id] AS MatchId,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                up.[Outcome]
            FROM [Matches] m
            INNER JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            INNER JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = @UserId
            WHERE m.[RoundId] IN @RoundIds
            ORDER BY m.[RoundId], m.[MatchDateTimeUtc] ASC, ht.[ShortName] ASC";

        var matches = await _dbConnection.QueryAsync<ActiveRoundMatchQueryResult>(
            matchesSql,
            cancellationToken,
            new { request.UserId, RoundIds = roundIds });

        // Group matches by RoundId for efficient lookup
        var matchesByRound = matches
            .GroupBy(m => m.RoundId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Map to DTOs
        return rounds.Select(r => new ActiveRoundDto(
            r.Id,
            r.SeasonName,
            r.RoundNumber,
            r.DeadlineUtc,
            r.HasUserPredicted,
            Enum.Parse<RoundStatus>(r.Status),
            matchesByRound.TryGetValue(r.Id, out var roundMatches)
                ? roundMatches.Select(m => new ActiveRoundMatchDto(
                    m.MatchId,
                    m.HomeTeamLogoUrl,
                    m.AwayTeamLogoUrl,
                    m.PredictedHomeScore,
                    m.PredictedAwayScore,
                    m.Outcome))
                : Enumerable.Empty<ActiveRoundMatchDto>()
        ));
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
        int MatchId,
        string? HomeTeamLogoUrl,
        string? AwayTeamLogoUrl,
        int? PredictedHomeScore,
        int? PredictedAwayScore,
        PredictionOutcome? Outcome);
}

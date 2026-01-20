using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetUpcomingRoundsQueryHandler : IRequestHandler<GetUpcomingRoundsQuery, IEnumerable<UpcomingRoundDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetUpcomingRoundsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<UpcomingRoundDto>> Handle(GetUpcomingRoundsQuery request, CancellationToken cancellationToken)
    {
        // Query 1: Get upcoming rounds
        const string roundsSql = @"
            WITH ActiveMemberCount AS (
                SELECT
                    l.[SeasonId],
                    COUNT(DISTINCT lm.[UserId]) AS MemberCount
                FROM [LeagueMembers] lm
                JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
                WHERE lm.[Status] = @ApprovedStatus
                GROUP BY l.[SeasonId]
            )

            SELECT
                r.[Id],
                s.[Name] AS SeasonName,
                r.[RoundNumber],
                r.[DeadlineUtc],
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
            LEFT JOIN
                [ActiveMemberCount] amc ON r.[SeasonId] = amc.[SeasonId]
            WHERE
                r.[Status] = @PublishedStatus
                AND r.[DeadlineUtc] > GETUTCDATE()
                AND r.[SeasonId] IN (
                    SELECT l.[SeasonId]
                    FROM [Leagues] l
                    JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
                    WHERE lm.[UserId] = @UserId AND lm.[Status] = @ApprovedStatus
                )
            ORDER BY
                r.[DeadlineUtc] ASC";

        var parameters = new
        {
            request.UserId,
            PublishedStatus = nameof(RoundStatus.Published),
            ApprovedStatus = nameof(LeagueMemberStatus.Approved)
        };

        var rounds = (await _dbConnection.QueryAsync<UpcomingRoundQueryResult>(
            roundsSql,
            cancellationToken,
            parameters)).ToList();

        if (!rounds.Any())
        {
            return Enumerable.Empty<UpcomingRoundDto>();
        }

        // Query 2: Get matches with predictions for all upcoming rounds
        var roundIds = rounds.Select(r => r.Id).ToArray();

        const string matchesSql = @"
            SELECT
                m.[RoundId],
                m.[Id] AS MatchId,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                m.[MatchDateTimeUtc],
                ht.[ShortName] AS HomeTeamShortName
            FROM [Matches] m
            INNER JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            INNER JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = @UserId
            WHERE m.[RoundId] IN @RoundIds
            ORDER BY m.[RoundId], m.[MatchDateTimeUtc] ASC, ht.[ShortName] ASC";

        var matches = await _dbConnection.QueryAsync<UpcomingMatchQueryResult>(
            matchesSql,
            cancellationToken,
            new { request.UserId, RoundIds = roundIds });

        // Group matches by RoundId for efficient lookup
        var matchesByRound = matches
            .GroupBy(m => m.RoundId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Map to DTOs
        return rounds.Select(r => new UpcomingRoundDto(
            r.Id,
            r.SeasonName,
            r.RoundNumber,
            r.DeadlineUtc,
            r.HasUserPredicted,
            matchesByRound.TryGetValue(r.Id, out var roundMatches)
                ? roundMatches.Select(m => new UpcomingMatchDto(
                    m.MatchId,
                    m.HomeTeamLogoUrl,
                    m.AwayTeamLogoUrl,
                    m.PredictedHomeScore,
                    m.PredictedAwayScore))
                : Enumerable.Empty<UpcomingMatchDto>()
        ));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UpcomingRoundQueryResult(
        int Id,
        string SeasonName,
        int RoundNumber,
        DateTime DeadlineUtc,
        bool HasUserPredicted);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UpcomingMatchQueryResult(
        int RoundId,
        int MatchId,
        string? HomeTeamLogoUrl,
        string? AwayTeamLogoUrl,
        int? PredictedHomeScore,
        int? PredictedAwayScore,
        DateTime MatchDateTimeUtc,
        string HomeTeamShortName);
}
